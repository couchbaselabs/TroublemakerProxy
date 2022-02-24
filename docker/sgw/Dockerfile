ARG SGW_VERSION=2.8.0-enterprise

FROM couchbase/sync-gateway:$SGW_VERSION

COPY entrypoint-shim.sh /entrypoint-shim.sh
ENTRYPOINT ["./entrypoint-shim.sh"]