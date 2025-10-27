#!/usr/bin/env bash

set -eu

# Start backend with credentials in background, log to backend.log
export GOOGLE_APPLICATION_CREDENTIALS="/home/ekwav/Downloads/conections-e5751-firebase-adminsdk-5lq43-8cb9eeefff.json"
echo "Starting backend (dotnet run)..."
# Kill any process listening on port 5042 to avoid address already in use
if lsof -i :5042 -t >/dev/null 2>&1; then
  echo "Port 5042 in use, killing existing process(es)"
  lsof -i :5042 -t | xargs -r kill
  sleep 1
fi

dotnet run --project /run/media/ekwav/Data2/dev/Con/ConApi/ConApi.csproj > backend.log 2>&1 &
BACKEND_PID=$!
echo "Backend PID: $BACKEND_PID"

echo "Waiting 10s for backend to start..."
sleep 15

echo "Running tests..."

curl 'http://localhost:5042/api/Search' \
  -H 'Accept: application/json' \
  -H 'Accept-Language: de-DE,de;q=0.9,en;q=0.8' \
  -H 'Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJqdGkiOiI2ZTM5ODAxNi04NjBkLTQ2ZmUtODdmNy1iYWYwNzA0MWZiMTUiLCJzdWIiOiJhOWZiNzYwNS00OGI0LTQwNjgtYTYyZC04Nzc5OWI4MGJiMDYiLCJleHAiOjE3NjQxMDUyODIsImlzcyI6Imh0dHBzOi8vY29uLmNvZmxuZXQuY29tIiwiYXVkIjoiaHR0cHM6Ly9jb24uY29mbG5ldC5jb20ifQ.Xvzgf0jOC26-q2aR6WyPXA7LMmLCNZo7cfNZGBiZttA' \
  -H 'Connection: keep-alive' \
  -H 'Origin: http://localhost:4208' \
  -H 'Referer: http://localhost:4208/' \
  -H 'Sec-Fetch-Dest: empty' \
  -H 'Sec-Fetch-Mode: cors' \
  -H 'Sec-Fetch-Site: same-site' \
  -H 'User-Agent: Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36' \
  -H 'sec-ch-ua: "Chromium";v="141", "Not?A_Brand";v="8"' \
  -H 'sec-ch-ua-mobile: ?0' \
  -H 'sec-ch-ua-platform: "Linux"' ;
curl 'http://localhost:5042/api/Person/39685570-108f-432a-bc12-198093950f91/full' \
  -X 'OPTIONS' \
  -H 'Accept: */*' \
  -H 'Accept-Language: de-DE,de;q=0.9,en;q=0.8' \
  -H 'Access-Control-Request-Headers: authorization' \
  -H 'Access-Control-Request-Method: GET' \
  -H 'Connection: keep-alive' \
  -H 'Origin: http://localhost:4208' \
  -H 'Referer: http://localhost:4208/' \
  -H 'Sec-Fetch-Dest: empty' \
  -H 'Sec-Fetch-Mode: cors' \
  -H 'Sec-Fetch-Site: same-site' \
  -H 'User-Agent: Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36' ;
curl 'http://localhost:5042/api/Person/39685570-108f-432a-bc12-198093950f91/full' \
  -H 'Accept: application/json' \
  -H 'Accept-Language: de-DE,de;q=0.9,en;q=0.8' \
  -H 'Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJqdGkiOiI2ZTM5ODAxNi04NjBkLTQ2ZmUtODdmNy1iYWYwNzA0MWZiMTUiLCJzdWIiOiJhOWZiNzYwNS00OGI0LTQwNjgtYTYyZC04Nzc5OWI4MGJiMDYiLCJleHAiOjE3NjQxMDUyODIsImlzcyI6Imh0dHBzOi8vY29uLmNvZmxuZXQuY29tIiwiYXVkIjoiaHR0cHM6Ly9jb24uY29mbG5ldC5jb20ifQ.Xvzgf0jOC26-q2aR6WyPXA7LMmLCNZo7cfNZGBiZttA' \
  -H 'Connection: keep-alive' \
  -H 'Origin: http://localhost:4208' \
  -H 'Referer: http://localhost:4208/' \
  -H 'Sec-Fetch-Dest: empty' \
  -H 'Sec-Fetch-Mode: cors' \
  -H 'Sec-Fetch-Site: same-site' \
  -H 'User-Agent: Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36' \
  -H 'sec-ch-ua: "Chromium";v="141", "Not?A_Brand";v="8"' \
  -H 'sec-ch-ua-mobile: ?0' \
  -H 'sec-ch-ua-platform: "Linux"'

echo "Tests finished. Capturing logs..."
sleep 1

echo "--- backend.log (last 200 lines) ---"
tail -n 200 backend.log || true

echo "--- End of backend.log ---"

echo "Stopping backend (PID $BACKEND_PID)"
kill ${BACKEND_PID} || true
wait ${BACKEND_PID} 2>/dev/null || true

echo "Done."