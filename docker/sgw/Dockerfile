ARG SGW_VERSION=2.8.0

FROM couchbase/sync-gateway:$SGW_VERSION-enterprise

COPY entrypoint-shim.sh /entrypoint-shim.sh
ENTRYPOINT ["./entrypoint-shim.sh"]