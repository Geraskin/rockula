FROM mcr.microsoft.com/dotnet/sdk:10.0

RUN apt-get update \
    && apt-get install -y --no-install-recommends git ca-certificates \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd --gid 1655 developer \
    && useradd --uid 1655 --gid developer --create-home --shell /bin/bash developer

# The previous devcontainer user and all four persistent volumes use UID:GID 1655:1655.
# Match both IDs to preserve access without mutating the existing volumes.
# This UID/GID is specific to RockULA, not a shared-template requirement.
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
