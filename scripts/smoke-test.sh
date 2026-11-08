#!/usr/bin/env bash
# Smoke test for a running Shipment Assistant.
# Start everything first:  docker compose up -d --build
# Then run (from anywhere):  bash scripts/smoke-test.sh

cd "$(dirname "$0")/.." || exit 1      # repository root, where docker-compose.yaml lives

API="${API:-http://localhost:5108/api}"
WEB="${WEB:-http://localhost:8080}"
TN="SH-9$(( RANDOM % 9000 + 1000 ))"   # a fresh tracking number for this run, e.g. SH-94821
BODY="$(mktemp)"
passed=0
failed=0

pass() { echo "PASS  $1"; passed=$((passed + 1)); }
fail() { echo "FAIL  $1"; failed=$((failed + 1)); }

# check <description> <expected HTTP status> <curl arguments...>
check() {
  local name="$1" expected="$2"
  shift 2
  local status
  status=$(curl -s -o "$BODY" -w "%{http_code}" --max-time 120 "$@")
  if [ "$status" = "$expected" ]; then
    pass "$name ($status)"
  else
    fail "$name: expected $expected, got $status"
    head -c 500 "$BODY"; echo
  fi
}

json=(-H "Content-Type: application/json")

echo "Smoke test, using tracking number $TN"
echo

echo "--- Web app and API"
check "Web app loads" 200 "$WEB"
check "List shipments" 200 "$API/shipments"
check "Unknown shipment returns 404" 404 "$API/shipments/SH-9999"

echo
echo "--- Creating and validating shipments"
new_shipment="{\"trackingNumber\":\"$TN\",\"origin\":\"London\",\"destination\":\"Vienna\"}"
check "Create shipment" 201 -X POST "${json[@]}" -d "$new_shipment" "$API/shipments"
check "Duplicate tracking number returns 409" 409 -X POST "${json[@]}" -d "$new_shipment" "$API/shipments"
check "Invalid tracking number returns 400" 400 -X POST "${json[@]}" \
  -d '{"trackingNumber":"ABC","origin":"London","destination":"Vienna"}' "$API/shipments"

echo
echo "--- Status changes"
for status in InTransit OutForDelivery Delivered; do
  check "Status -> $status" 200 -X PATCH "${json[@]}" -d "{\"status\":\"$status\"}" "$API/shipments/$TN/status"
done
check "Delivered is final (409)" 409 -X PATCH "${json[@]}" -d '{"status":"InTransit"}' "$API/shipments/$TN/status"

echo
echo "--- Messaging (RabbitMQ -> notifications worker)"
sleep 3
if docker compose logs notifications --since 5m 2>/dev/null | grep -q "$TN"; then
  pass "Notification sent for $TN"
else
  fail "No notification for $TN in the worker log (check: docker compose logs notifications)"
fi

echo
echo "--- AI assistant (can take 10-30 seconds on the first question)"
check "Answer from shipment data" 200 -X POST "${json[@]}" \
  -d '{"question":"Where is SH-1001?"}' "$API/assistant/ask"
echo "      Answer: $(head -c 300 "$BODY")"
check "Answer from policy documents (RAG)" 200 -X POST "${json[@]}" \
  -d '{"question":"What happens if nobody is home?"}' "$API/assistant/ask"
echo "      Answer: $(head -c 300 "$BODY")"

rm -f "$BODY"
echo
echo "$passed passed, $failed failed"
[ "$failed" -eq 0 ]