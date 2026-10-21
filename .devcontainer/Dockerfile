FROM mcr.microsoft.com/dotnet/sdk:10.0

RUN apt-get update \
    && apt-get install -y --no-install-recommends git ca-certificates \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --create-home --shell /bin/bash vscode

# Persistent Copilot CLI state (preserve existing local setup).
RUN mkdir -p /home/vscode/.copilot \
    && chown vscode:vscode /home/vscode/.copilot \
    && chmod 700 /home/vscode/.copilot

# OpenCode uses separate config, data and state directories.
# Create them as vscode before mounting named volumes to avoid root-owned mounts.
RUN mkdir -p /home/vscode/.local/share/opencode \
    /home/vscode/.local/state/opencode \
    /home/vscode/.config/opencode \
    && chown -R vscode:vscode /home/vscode/.local /home/vscode/.config

USER vscode
WORKDIR /workspaces/rockula
