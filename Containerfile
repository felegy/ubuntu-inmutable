FROM ubuntu:24.04

ARG IMAGE_CREATED
ARG IMAGE_REVISION

LABEL org.opencontainers.image.title="Ubuntu Inmutable OS image"
LABEL org.opencontainers.image.description="Base Ubuntu immutable-oriented image scaffold"
LABEL org.opencontainers.image.created="$IMAGE_CREATED"
LABEL org.opencontainers.image.revision="$IMAGE_REVISION"

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates tzdata \
    && rm -rf /var/lib/apt/lists/*

RUN groupadd --gid 10001 app \
    && useradd --uid 10001 --gid 10001 --create-home --shell /usr/sbin/nologin app

WORKDIR /workspace
USER app

CMD ["/bin/bash"]
