# Linux dedicated server image for Steading.
#
# Build with: docker build -t steading-server .
# Run with:   docker run -p 7777:7777/udp -v ./world_data:/app/world_data steading-server
#
# Prereq: produce a Linux server build from Unity (File -> Build Settings ->
# Linux -> Server Build checked). Place the output in Server/build/ before
# running `docker build`.

FROM ubuntu:22.04

RUN apt-get update && apt-get install -y --no-install-recommends \
        ca-certificates \
        libgcc-s1 \
        libstdc++6 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY build/ /app/

RUN chmod +x /app/Steading-Server.x86_64

EXPOSE 7777/udp

VOLUME ["/app/world_data"]

ENV STEADING_PORT=7777
ENV STEADING_WORLD_NAME=default

ENTRYPOINT ["/app/Steading-Server.x86_64", "-batchmode", "-nographics", "-server"]
CMD ["-port", "7777"]
