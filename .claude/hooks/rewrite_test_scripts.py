"""PreToolUse hook: rewrite run-tests.bat / smoke-test.bat into their -summary versions.

Reads the tool call JSON from stdin. If the command invokes one of the raw scripts, emits
`updatedInput` with the command rewritten so the session only ever sees the summary. The raw
scripts stay available to the developer from a normal terminal.
"""
import json
import re
import sys


def rewrite(command: str) -> str:
    # Match run-tests.bat / smoke-test.bat that are not already the -summary variant,
    # with any path prefix and either slash style.
    pattern = re.compile(r"(?<![\w-])(run-tests|smoke-test)(?!-summary)\.bat", re.IGNORECASE)
    return pattern.sub(lambda m: m.group(1) + "-summary.bat", command)


def main() -> int:
    try:
        payload = json.load(sys.stdin)
    except Exception:
        return 0
    tool_input = payload.get("tool_input") or {}
    command = tool_input.get("command")
    if not isinstance(command, str):
        return 0
    new_command = rewrite(command)
    if new_command == command:
        return 0
    updated = dict(tool_input)
    updated["command"] = new_command
    print(json.dumps({
        "hookSpecificOutput": {
            "hookEventName": "PreToolUse",
            "permissionDecision": "allow",
            "permissionDecisionReason": "Rewrote raw test script to its -summary version (token hygiene).",
            "updatedInput": updated,
        }
    }))
    return 0


if __name__ == "__main__":
    sys.exit(main())
