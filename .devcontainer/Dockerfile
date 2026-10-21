FROM mcr.microsoft.com/dotnet/sdk:10.0

RUN apt-get update \
    && apt-get install -y --no-install-recommends git ca-certificates \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --uid 1000 --create-home --shell /bin/bash developer

# Persistent Copilot CLI state. UID 1000 retains access to existing named volumes.
RUN mkdir -p /home/developer/.copilot \
    && chown developer:developer /home/developer/.copilot \
    && chmod 700 /home/developer/.copilot

# OpenCode uses separate config, data and state directories.
# Create them as developer before mounting named volumes to avoid root-owned mounts.
RUN mkdir -p /home/developer/.local/share/opencode \
    /home/developer/.local/state/opencode \
    /home/developer/.config/opencode \
    && chown -R developer:developer /home/developer/.local /home/developer/.config

USER developer
WORKDIR /workspaces/rockula
