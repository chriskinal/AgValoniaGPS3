#!/bin/bash
# Launch parallel Claude Code agents in Docker containers
# Each agent gets its own git worktree and task prompt
#
# Usage:
#   ./launch-agents.sh                    # interactive: prompts for tasks
#   ./launch-agents.sh tasks.txt          # batch: reads tasks from file
#
# tasks.txt format (one task per line, tab-separated: branch-name<TAB>prompt):
#   feature/hotkeys    Implement the hotkey configuration feature as described in CONTRIBUTING.md
#   feature/headland   Implement the headland builder feature as described in CONTRIBUTING.md

set -e

REPO_DIR="$(cd "$(dirname "$0")" && pwd)"
PARENT_DIR="$(dirname "$REPO_DIR")"
CLAUDE_AUTH_DIR="/mnt/c/Users/mnuuj/.claude"
IMAGE_NAME="agvalonia-claude"
TASKS_FILE="${1:-}"

# Build Docker image if not already built
build_image() {
    if ! docker image inspect "$IMAGE_NAME" &>/dev/null; then
        echo "Building Docker image..."
        docker build -f "$REPO_DIR/Dockerfile.claude" -t "$IMAGE_NAME" "$REPO_DIR"
    else
        echo "Docker image already exists. Run 'docker rmi $IMAGE_NAME' to rebuild."
    fi
}

# Create a worktree and launch a container for it
launch_agent() {
    local branch="$1"
    local prompt="$2"
    local worktree_name="${branch//\//-}"  # replace / with -
    local worktree_dir="$PARENT_DIR/$worktree_name"
    local container_name="claude-${worktree_name}"

    echo ""
    echo "=== Launching agent: $branch ==="

    # Create git worktree if it doesn't exist
    if [ ! -d "$worktree_dir" ]; then
        echo "Creating worktree at $worktree_dir..."
        cd "$REPO_DIR"
        git worktree add -b "$branch" "$worktree_dir" master
    else
        echo "Worktree already exists at $worktree_dir"
    fi

    # Stop and remove existing container if running
    docker rm -f "$container_name" 2>/dev/null || true

    # Launch container in background
    echo "Starting container $container_name..."
    docker run -d \
        --name "$container_name" \
        -v "$worktree_dir:/workspace" \
        -v "$CLAUDE_AUTH_DIR:/root/.claude:ro" \
        -e "CLAUDE_TASK=$prompt" \
        "$IMAGE_NAME" \
        bash -c "cd /workspace && claude --dangerously-skip-permissions -p \"\$CLAUDE_TASK\""

    echo "Container started. Logs: docker logs -f $container_name"
}

# Interactive mode: ask user for tasks
interactive_mode() {
    echo "Interactive mode. Enter tasks (empty line to finish):"
    echo "Format: branch-name | task description"
    echo ""

    declare -a branches
    declare -a prompts

    while true; do
        read -r -p "Task (or empty to start): " line
        [ -z "$line" ] && break

        branch=$(echo "$line" | cut -d'|' -f1 | xargs)
        prompt=$(echo "$line" | cut -d'|' -f2- | xargs)

        if [ -z "$branch" ] || [ -z "$prompt" ]; then
            echo "Invalid format. Use: branch-name | task description"
            continue
        fi

        branches+=("$branch")
        prompts+=("$prompt")
    done

    for i in "${!branches[@]}"; do
        launch_agent "${branches[$i]}" "${prompts[$i]}"
    done
}

# Batch mode: read tasks from file
batch_mode() {
    local file="$1"
    echo "Reading tasks from $file..."

    while IFS=$'\t' read -r branch prompt || [ -n "$branch" ]; do
        # Skip empty lines and comments
        [[ -z "$branch" || "$branch" == \#* ]] && continue
        launch_agent "$branch" "$prompt"
    done < "$file"
}

# Status: show running agents
status() {
    echo ""
    echo "=== Running Claude agents ==="
    docker ps --filter "name=claude-" --format "table {{.Names}}\t{{.Status}}\t{{.RunningFor}}"
}

# Main
build_image

if [ -n "$TASKS_FILE" ] && [ -f "$TASKS_FILE" ]; then
    batch_mode "$TASKS_FILE"
else
    interactive_mode
fi

status
echo ""
echo "Attach to an agent: docker attach <container-name>"
echo "View logs:          docker logs -f <container-name>"
echo "Stop all agents:    docker ps -q --filter name=claude- | xargs docker stop"
