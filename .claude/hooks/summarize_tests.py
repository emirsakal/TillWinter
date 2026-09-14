"""Summarise a Unity EditMode test run: compile errors, counts, first 5 message lines per failure.

Usage: summarize_tests.py <results.xml> <editor.log> <exit code>
Output is capped at 100 lines.
"""
import os
import re
import sys
import xml.etree.ElementTree as ET

MAX_LINES = 100


def main() -> int:
    xml_path, log_path = sys.argv[1], sys.argv[2]
    exit_code = int(sys.argv[3]) if len(sys.argv) > 3 else 0
    lines = []

    # Compile errors (deduplicated, in order).
    if os.path.exists(log_path):
        seen = set()
        with open(log_path, encoding="utf-8", errors="replace") as f:
            for raw in f:
                m = re.match(r"^(.*?)\((\d+),\d+\): error (CS\d+): (.*)$", raw.strip())
                if m and m.group(0) not in seen:
                    seen.add(m.group(0))
                    lines.append(f"ERROR {m.group(1)}:{m.group(2)} {m.group(3)} {m.group(4)}")

    if not os.path.exists(xml_path):
        lines.append("No results XML written (compile error or Unity could not start). Exit code %d." % exit_code)
        emit(lines)
        return 0

    root = ET.parse(xml_path).getroot()
    lines.append("Tests: %s passed, %s failed, %s total (%s)" % (
        root.get("passed", "?"), root.get("failed", "?"), root.get("total", "?"), root.get("result", "?")))

    for tc in root.iter("test-case"):
        if tc.get("result") == "Passed":
            continue
        lines.append("FAIL " + (tc.get("fullname") or tc.get("name") or "?"))
        failure = tc.find("failure")
        if failure is not None:
            msg = (failure.findtext("message") or "").strip().splitlines()
            for m in msg[:5]:
                lines.append("    " + m)
            trace = (failure.findtext("stack-trace") or "").strip().splitlines()
            if trace:
                lines.append("    at " + trace[0].strip()[:160])
    lines.append("Exit code %d" % exit_code)
    emit(lines)
    return 0


def emit(lines):
    if len(lines) > MAX_LINES:
        lines = lines[:MAX_LINES - 1] + ["... (%d more lines truncated)" % (len(lines) - MAX_LINES + 1)]
    for line in lines:
        print(line)


if __name__ == "__main__":
    sys.exit(main())
