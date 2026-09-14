"""Summarise TestResults/smoke/report.txt: FAIL lines, console-error count, final result line.

Usage: summarize_smoke.py <report.txt> <exit code>
"""
import os
import sys


def main() -> int:
    path = sys.argv[1]
    exit_code = int(sys.argv[2]) if len(sys.argv) > 2 else 0
    if not os.path.exists(path):
        print("No smoke report written (editor failed to start or project already open). Exit code %d." % exit_code)
        return 0
    fails, console, done, passes = [], 0, None, 0
    with open(path, encoding="utf-8", errors="replace") as f:
        for raw in f:
            line = raw.rstrip()
            body = line.split("] ", 1)[-1] if "] " in line else line
            if body.startswith("FAIL"):
                fails.append(line)
            elif body.startswith("PASS"):
                passes += 1
            elif body.startswith("CONSOLE"):
                console += 1
                if len(fails) < 20:
                    fails.append(line[:200])
            elif body.startswith("Done."):
                done = line
    print("Smoke: %d checks passed, %d failed, %d console errors" % (passes, len([f for f in fails if "FAIL" in f]), console))
    for f in fails[:40]:
        print(f)
    if done:
        print(done)
    print("Exit code %d" % exit_code)
    if fails:
        print("Screenshots for failed checks: TestResults\\smoke\\<NN-name>.png (open only the one named in the failed step).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
