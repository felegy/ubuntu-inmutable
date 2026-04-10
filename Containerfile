ARG BASE_IMAGE=ubuntu:24.04
ARG KAIROS_INIT_VERSION=0.7.0
ARG KAIROS_INIT_LOG_LEVEL=debug

FROM quay.io/kairos/kairos-init:v${KAIROS_INIT_VERSION} AS kairos-init

FROM ${BASE_IMAGE} AS base-kairos
ARG DEFAULT_USER=kairos
ARG DEBIAN_FRONTEND=noninteractive
ARG VARIANT=core
ARG MODEL=generic
ARG TRUSTED_BOOT=false
ARG VERSION
ARG KUBERNETES_DISTRO
ARG KUBERNETES_VERSION=${VERSION}
ARG KAIROS_INIT_LOG_LEVEL=debug
ARG DEBS='open-vm-tools'

RUN apt-get update && apt-get -y --no-install-recommends install \
    curl \
    ca-certificates \
    locales \
    nfs-common; \
    apt-get -y --no-install-recommends install \
    ${DEBS}; \
    apt-get clean && rm -rf /var/lib/apt/lists/*; \
    sed -i -e 's/# en_US.UTF-8 UTF-8/en_US.UTF-8 UTF-8/' /etc/locale.gen; \
    sed -i -e 's/# hu_HU.UTF-8 UTF-8/hu_HU.UTF-8 UTF-8/' /etc/locale.gen

RUN --mount=type=bind,from=kairos-init,src=/kairos-init,dst=/kairos-init \
    if [ -n "${KUBERNETES_DISTRO}" ]; then \
        K8S_FLAG="-p ${KUBERNETES_DISTRO}"; \
        if [ "${KUBERNETES_DISTRO}" = "k0s" ] && [ -n "${KUBERNETES_VERSION}" ]; then \
            K8S_VERSION_FLAG="--provider-k0s-version \"${KUBERNETES_VERSION}\""; \
        elif [ "${KUBERNETES_DISTRO}" = "k3s" ] && [ -n "${KUBERNETES_VERSION}" ]; then \
            K8S_VERSION_FLAG="--provider-k3s-version \"${KUBERNETES_VERSION}\""; \
        else \
            K8S_VERSION_FLAG=""; \
        fi; \
    else \
        K8S_FLAG=""; \
        K8S_VERSION_FLAG=""; \
    fi; \
    eval /kairos-init -l "${KAIROS_INIT_LOG_LEVEL}" -s install -m "${MODEL}" -v "${VARIANT}" -t "${TRUSTED_BOOT}" --version "${VERSION}" ${K8S_FLAG} ${K8S_VERSION_FLAG}; \
    eval /kairos-init -l "${KAIROS_INIT_LOG_LEVEL}" -s init -m "${MODEL}" -v "${VARIANT}" -t "${TRUSTED_BOOT}" --version "${VERSION}"  ${K8S_FLAG} ${K8S_VERSION_FLAG}; \
    eval /kairos-init validate -t "${TRUSTED_BOOT}"; \
    locale-gen en_US.UTF-8; \
    update-locale LANG=en_US.UTF-8; \
    echo "LANG=en_US.UTF-8" > /etc/default/locale;

COPY root-fs /
RUN \
    if [ -f /system/oem/10_accounting.yaml ]; then \
        sed -i "s/kairos/${DEFAULT_USER}/g" /system/oem/10_accounting.yaml; \
    fi;

FROM ${BASE_IMAGE} AS final-kairos

ARG IMAGE_CREATED
ARG IMAGE_REVISION

LABEL org.opencontainers.image.title="Ubuntu Inmutable OS image"
LABEL org.opencontainers.image.description="Base Ubuntu immutable-oriented image scaffold"
LABEL org.opencontainers.image.created="$IMAGE_CREATED"
LABEL org.opencontainers.image.revision="$IMAGE_REVISION"


COPY --from=base-kairos / /
