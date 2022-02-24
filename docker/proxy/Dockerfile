FROM snowstep/apt-fast

ARG troublemaker_version

RUN test -n $troublemaker_version || (echo "$troublemaker_version not set" && false)

RUN export DEBIAN_FRONTEND=noninteractive \
    && apt-fast update \
    && apt-fast install -y --no-install-recommends \
       wget \
       ca-certificates \
    && rm -rf /var/lib/apt/lists/*

RUN wget "https://github.com/couchbaselabs/TroublemakerProxy/releases/download/v$troublemaker_version/TroublemakerProxy-v$troublemaker_version-linux-x64.tar.gz" -O /usr/local/bin/TroubleMaker.tar.gz && \
    cd /usr/local/bin && \
    tar xvf TroubleMaker.tar.gz

ENV PATH="/usr/local/bin/TroublemakerProxy-v$troublemaker_version-linux-x64:${PATH}"

ENTRYPOINT ["TroublemakerProxy", "-c", "/etc/troublemaker/config.json"]