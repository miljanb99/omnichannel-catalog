#!/usr/bin/env bash
# Skriptovani scenario za odbranu. Vodi narativ kroz REST API dok je kontrolna
# tabla otvorena na http://localhost:8080 (promene se vide uzivo preko SignalR-a).
#
# Upotreba:
#   ./scenario/demo.sh            # interaktivno, pauza posle svakog koraka
#   AUTO=1 ./scenario/demo.sh     # bez pauza (proba pre odbrane)
#   BASE=http://localhost:8080 ./scenario/demo.sh
set -euo pipefail
BASE="${BASE:-http://localhost:8080}"
AUTO="${AUTO:-}"

pause() { if [ -z "$AUTO" ]; then echo; read -rp "  ⏎ za sledeci korak: "; else sleep 1; fi; echo; }
step()  { echo; echo "▶ $1"; }
note()  { echo "    $1"; }

post()  { curl -fsS -X POST "$BASE$1" -H 'Content-Type: application/json' ${2:+-d "$2"} >/dev/null; }
put()   { curl -fsS -X PUT  "$BASE$1" -H 'Content-Type: application/json' -d "$2" >/dev/null; }

# stanje jednog listinga: namera / glas kanala / rezultat pomirenja
show() {
  curl -fsS "$BASE/api/current/listings" | python3 -c '
import sys, json
lid = sys.argv[1]
rows = [x for x in json.load(sys.stdin) if x["entityId"] == lid]
if not rows:
    print("    (listing ne postoji)"); sys.exit()
l = rows[0]; p = l["price"]
draft = p["draft"] if p["hasDraft"] else "-"
print("    cena: aktivno=%s nacrt=%s | kanal: %s obs=%s | zelja=%s | objava=%s%s" % (
    p["active"], draft, l["effectiveStatus"], l["observedPrice"],
    l["desiredStatus"], l["publishStatus"],
    "  (" + l["moderationNote"] + ")" if l.get("moderationNote") else ""))
' "$1"
}

summary() {
  curl -fsS "$BASE/api/current" | python3 -c '
import sys, json
from collections import Counter
d = json.load(sys.stdin)
c = Counter(x["publishStatus"] for x in d["listings"])
print("    proizvodi=%d varijante=%d listinzi=%d  ->  %s" % (
    len(d["products"]), len(d["variants"]), len(d["listings"]),
    ", ".join("%s %d" % (k, v) for k, v in sorted(c.items())) or "prazno"))
' ; }

# ceka da nijedan listing ne bude vise u stanju pending (kanal je sve potvrdio)
wait_converged() {
  for _ in $(seq 1 20); do
    if curl -fsS "$BASE/api/current/listings" | python3 -c '
import sys, json
sys.exit(0 if not [x for x in json.load(sys.stdin) if x["publishStatus"] == "pending"] else 1)'; then
      return 0
    fi
    sleep 0.5
  done
}

first_listing_on() {  # $1 = naziv kanala
  curl -fsS "$BASE/api/current/listings" | python3 -c '
import sys, json
ch = sys.argv[1]
rows = [x for x in json.load(sys.stdin) if x["channel"] == ch and not x["removed"]]
print(rows[0]["entityId"])
' "$1"
}

echo "════════════════════════════════════════════════════════════════"
echo "  MongoDB Change Streams — demonstracija reaktivnog kataloga"
echo "  tabla: $BASE     (drzi je otvorenu pored terminala)"
echo "════════════════════════════════════════════════════════════════"

step "0) Cist pocetak — brisemo logove, tekuce stanje i resume tokene"
post "/api/admin/simulator" '{"enabled":true}'
post "/api/admin/reset"
summary
pause

step "1) Ucitavanje kataloga — 17 modela patika -> 19 varijanti -> 43 listinga"
note "upis ide iskljucivo u append-only logove; tekuce stanje jos ne postoji"
post "/api/admin/seed?dataset=patike"
sleep 1
note "posle ~1s (materijalizacija kroz change stream, kanal jos nije potvrdio):"
summary
note "cekamo da simulirani kanal potvrdi sve listinge…"
wait_converged
summary
note "sopstvena prodavnica objavljuje odmah, marketplace kanali prvo idu u pregled"
pause

step "2) Pauziramo kanal — da bismo mogli da rezirali svaki sledeci korak"
post "/api/admin/simulator" '{"enabled":false}'
note "od sada nijedan nacrt nece biti automatski potvrdjen"
pause

LID=$(first_listing_on "Ananas")
step "3) Trgovac menja cenu — nastaje NACRT, aktivna cena se ne dira"
note "listing: $LID"
show "$LID"
put "/api/listings/$LID" '{"price":24990}'
sleep 0.6
show "$LID"
note "kupac i dalje vidi staru cenu; nova postoji kao namera -> objava=pending"
pause

step "4) Kanal ODBIJA izmenu — nacrt prezivljava odbijanje"
post "/api/channel/$LID/reject" '{"note":"Promena je odbijena, postavi novu"}'
sleep 0.6
show "$LID"
note "objava=rejected, ali nacrt 24990 i dalje ceka — namera nije izgubljena"
pause

step "5) Kanal se predomislio i NAKNADNO prihvata istu izmenu"
curl -fsS -X POST "$BASE/api/channel/$LID/observe" -H 'Content-Type: application/json' \
  -d '{"effectiveStatus":"active","observedPrice":24990,"available":true}' >/dev/null
sleep 0.6
show "$LID"
note "posmatrana cena == nacrt -> Publish(): nacrt postaje aktivna cena, objava=published"
pause

step "6) Odustajanje od izmene — nacrt se odbacuje pre potvrde"
put "/api/listings/$LID" '{"price":31990}'
sleep 0.6
show "$LID"
note "sada odustajemo:"
post "/api/listings/$LID/discard-draft"
sleep 0.6
show "$LID"
note "vraceno na ono sto je kanal poslednje potvrdio — ponistena je samo nasa namera"
pause

step "7) Povlacenje artikla — dvostrano, kanal mora da potvrdi skidanje"
put "/api/listings/$LID" '{"desiredStatus":"withdrawn"}'
sleep 0.6
show "$LID"
note "zelja je withdrawn, ali kanal jos uvek prijavljuje active — oglas jos visi"
note "pustamo kanal da odgovori:"
post "/api/admin/simulator" '{"enabled":true}'
sleep 3
show "$LID"
note "kanal je javio paused — tek sada je artikal stvarno skinut"
pause

step "8) Vracanje u prodaju"
put "/api/listings/$LID" '{"desiredStatus":"published"}'
sleep 3
show "$LID"
note "zelja published -> objava pending -> kanal potvrdjuje -> published"
pause

step "9) Replay — tekuce stanje je izvediva velicina"
note "brisemo sve materijalizovane poglede i gradimo ih ponovo iz logova…"
curl -fsS -X POST "$BASE/api/admin/replay" | python3 -c '
import sys, json
print("    replay:", json.load(sys.stdin))'
sleep 1
wait_converged
summary
show "$LID"
note "isti brojevi i isti status — deterministicka rekonstrukcija iz istorije dogadjaja"
pause

step "10) Otpornost — nastavak od resume tokena posle pada projektora"
KEYS=$(curl -fsS "$BASE/api/current/listings" | python3 -c '
import sys, json
lid = sys.argv[1]
l = [x for x in json.load(sys.stdin) if x["entityId"] == lid][0]
print("%s|%s" % (l["productId"], l["variantId"]))
' "$LID")
PRD="${KEYS%%|*}"; VAR="${KEYS##*|}"

note "pregled logova i kontrolnih tacaka u bazi (docker mongo je na portu 27018):"
note "  mongosh 'mongodb://localhost:27018/omnichannelCatalog?directConnection=true'"
note "  db.listingsProprietaryStates.find({entityId:\"$LID\"}).sort({createdAt:1})"
note "  db.resumeTokens.find()"
echo
note "scenario pada — pokrenuti u drugom terminalu, redom:"
echo
echo "  docker compose stop host"
echo
echo "  docker compose exec -T mongo mongosh --quiet omnichannelCatalog --eval '"
echo "    const now = new Date();"
echo "    db.listingsProprietaryStates.insertMany(["
echo "      { entityId:\"$LID\", productId:\"$PRD\", variantId:\"$VAR\","
echo "        channel:\"Ananas\", price:NumberDecimal(\"39990\"), removed:false,"
echo "        discardDraft:false, createdAt:now },"
echo "      { entityId:\"$LID\", productId:\"$PRD\", variantId:\"$VAR\","
echo "        channel:\"Ananas\", price:NumberDecimal(\"41990\"), removed:false,"
echo "        discardDraft:false, createdAt:new Date(now.getTime()+1000) }"
echo "    ]);'"
echo
echo "  docker compose start host"
echo
note "ocekivano: projektor nastavlja od resume tokena, primenjuje OBA dogadjaja u"
note "redosledu upisa, ostaje TACNO JEDAN dokument tekuceg stanja (bez duplikata),"
note "a poslednja cena (41990) postaje aktivna cim je kanal potvrdi."
echo
note "varijanta sa gubitkom tacke oslonca (grana za oplog rollover):"
note "  POST /api/admin/drop-resume-tokens  pa  docker compose restart host"

echo
echo "✔ Kraj scenarija. Kanal je ostavljen ukljucen."
post "/api/admin/simulator" '{"enabled":true}'
