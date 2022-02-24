ARG COUCHBASE_VERSION=7.0.2

FROM couchbase/server:enterprise-$COUCHBASE_VERSION

COPY configure-node.sh /etc/service/config-couchbase/run
RUN chown -R couchbase:couchbase /etc/service