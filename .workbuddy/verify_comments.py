#!/usr/bin/env python3
"""校验"仅添加注释"改动：剥离注释与空白后比较代码字符流是否完全一致。"""
import sys
from pathlib import Path

BACKUP = Path(__file__).resolve().parent / "_backup_cs"
ROOT = BACKUP.parent.parent


def strip_comments(src: str) -> str:
    out = []
    i = 0
    n = len(src)
    while i < n:
        c = src[i]
        nxt = src[i + 1] if i + 1 < n else ""
        # 行注释
        if c == "/" and nxt == "/":
            while i < n and src[i] != "\n":
                i += 1
            continue
        # 块注释
        if c == "/" and nxt == "*":
            i += 2
            while i < n - 1 and not (src[i] == "*" and src[i + 1] == "/"):
                i += 1
            i += 2
            continue
        # 逐字字符串 @"..."
        if c == "@" and nxt == '"':
            out.append('@"')
            i += 2
            while i < n:
                if src[i] == '"':
                    if i + 1 < n and src[i + 1] == '"':
                        out.append('""')
                        i += 2
                        continue
                    out.append('"')
                    i += 1
                    break
                out.append(src[i])
                i += 1
            continue
        # 普通/插值字符串
        if c == '"' or (c == "$" and nxt == '"'):
            if c == "$":
                out.append("$")
                i += 1
                continue
            out.append('"')
            i += 1
            while i < n:
                if src[i] == "\\":
                    out.append(src[i:i + 2])
                    i += 2
                    continue
                if src[i] == '"':
                    out.append('"')
                    i += 1
                    break
                out.append(src[i])
                i += 1
            continue
        # 字符字面量
        if c == "'":
            out.append("'")
            i += 1
            while i < n:
                if src[i] == "\\":
                    out.append(src[i:i + 2])
                    i += 2
                    continue
                if src[i] == "'":
                    out.append("'")
                    i += 1
                    break
                out.append(src[i])
                i += 1
            continue
        out.append(c)
        i += 1
    return "".join(out)


def norm(path: Path) -> str:
    text = path.read_text(encoding="utf-8-sig", errors="replace")
    text = strip_comments(text)
    return "".join(text.split())


def main() -> int:
    # 可选参数：只校验指定路径前缀（便于分批并行处理时各自自检）
    prefixes = [a.replace("\\", "/").strip("/") for a in sys.argv[1:]]
    changed_files = []
    bad = []
    for bak in BACKUP.rglob("*.cs"):
        rel = bak.relative_to(BACKUP)
        rel_posix = rel.as_posix()
        if prefixes and not any(rel_posix.startswith(p) for p in prefixes):
            continue
        cur = ROOT / rel
        if not cur.exists():
            bad.append((rel, "文件丢失"))
            continue
        if bak.read_bytes() == cur.read_bytes():
            continue
        changed_files.append(rel)
        try:
            if norm(bak) != norm(cur):
                bad.append((rel, "代码内容发生变化"))
        except Exception as exc:  # noqa: BLE001
            bad.append((rel, f"读取失败: {exc}"))

    print(f"已改动文件: {len(changed_files)}")
    if bad:
        print(f"❌ 异常文件: {len(bad)}")
        for rel, reason in bad:
            print(f"  - {rel}: {reason}")
        return 1
    print("✅ 所有改动文件的代码内容（剥离注释后）完全一致")
    return 0


if __name__ == "__main__":
    sys.exit(main())
