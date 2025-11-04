# Workflow

1. Run `powershell -ExecutionPolicy Bypass -File tools/start_agent.ps1` at the start of each session to surface notes and priorities.
2. Use `powershell -ExecutionPolicy Bypass -File tools/manage_todo.ps1` (with `--complete` when appropriate) to advance todo items.
3. After completing a focused burst of work, update `agents.md`, `next.md`, or `todo.md` via the scripts rather than manual edits.
4. Commit in small increments detailing the change set.
