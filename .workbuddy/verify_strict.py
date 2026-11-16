#!/usr/bin/env python3
"""严格校验：逐行 diff，要求所有差异只能是新增的注释行，
或既有行末尾追加注释（剥离行尾注释后与原行完全一致）。"""
import difflib
import sys
from pathlib import Path

BACKUP = Path(__file__).resolve().parent / "_backup_cs"
ROOT = BACKUP.parent.parent


def strip_line_comment(line: str) -> str:
    s = line.rstrip()
    idx = s.find("//")
    if idx == -1:
        return s
    # 统计引号：若 // 位于字符串内则不剥离（本项目场景足够）
    if s[:idx].count('"') % 2 == 1:
        return s
    return s[:idx].rstrip()


def main() -> int:
    bad = []
    total_added = 0
    for bak in BACKUP.rglob("*.cs"):
        rel = bak.relative_to(BACKUP)
        cur = ROOT / rel
        if bak.read_bytes() == cur.read_bytes():
            continue
        old = bak.read_text(encoding="utf-8-sig").splitlines()
        new = cur.read_text(encoding="utf-8-sig").splitlines()
        sm = difflib.SequenceMatcher(None, old, new, autojunk=False)
        for tag, i1, i2, j1, j2 in sm.get_opcodes():
            if tag == "equal":
                continue
            if tag == "insert":
                # 新增行必须是纯注释行
                for line in new[j1:j2]:
                    t = line.strip()
                    if t.startswith("///") or t.startswith("//") or t.startswith("/*") or t == "":
                        total_added += 1
                    else:
                        bad.append((rel, f"新增了非注释行: {t[:80]}"))
            elif tag == "replace":
                olds = old[i1:i2]
                news = new[j1:j2]
                # 允许：旧代码行 + 行尾追加注释、其间夹杂纯注释行/空行
                mapped = []
                ok = True
                for line in news:
                    t = line.strip()
                    s = strip_line_comment(line)
                    if t == "" or t.startswith("///") or t.startswith("//") or t.startswith("/*"):
                        if s == "":
                            continue  # 纯注释/空行
                    if s == "":
                        continue  # 行尾全是注释
                    mapped.append(s)
                if [o.rstrip() for o in olds] == mapped and len(mapped) > 0:
                    total_added += len(news) - len(mapped)
                    continue
                bad.append((rel, f"replace 块 i{i1}:{i2} j{j1}:{j2}"))
            else:  # delete
                bad.append((rel, f"delete 块 i{i1}:{i2}"))

    print(f"纯注释新增行总数: {total_added}")
    if bad:
        print(f"❌ 异常文件 {len(set(r for r, _ in bad))} 个：")
        seen = set()
        for rel, msg in bad:
            if rel in seen:
                continue
            seen.add(rel)
            print(f"  - {rel}: {msg}")
        return 1
    print("✅ 严格校验通过：所有差异均为纯注释新增，零代码行改动")
    return 0


if __name__ == "__main__":
    sys.exit(main())
