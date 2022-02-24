#!/bin/sh

wait_for_uri() {
  expected=$1
  shift
  uri=$1
  echo "Waiting for $uri to be available..."
  while true; do
    status=$(curl -s -w "%{http_code}" -o /dev/null $*)
    if [ "x$status" = "x$expected" ]; then
      break
    fi
    echo "$uri not up yet, waiting 2 seconds..."
    sleep 2
  done
  echo "$uri ready, continuing"
}

wait_for_uri 200 http://cb-server:8091/pools/default/buckets/db -u admin:password
echo "Sleeping for 15 seconds to give server time to settle..."
sleep 15
echo "...done!"

./entrypoint.sh "$@"