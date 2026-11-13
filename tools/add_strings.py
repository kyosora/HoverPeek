"""把新字串同時加進 Strings.resx（繁中）、Strings.en.resx（英文）與 Strings.cs。

用法：python tools/add_strings.py strings.json
strings.json 格式：{"Key": ["繁中", "English"], ...}
已存在的 key 會更新內容，不會重複新增。
"""
import io, json, re, sys
from xml.sax.saxutils import escape

ROOT = "src/HoverPeek.Core/Localization/"

def upsert_resx(path, key, value):
    s = io.open(path, encoding="utf-8-sig").read()
    entry = f'  <data name="{key}" xml:space="preserve">\n    <value>{escape(value)}</value>\n  </data>\n'
    pattern = re.compile(rf'  <data name="{re.escape(key)}"[^>]*>.*?</data>\n', re.S)
    if pattern.search(s):
        s = pattern.sub(lambda _: entry, s, count=1)
    else:
        idx = s.rindex("</root>")
        s = s[:idx] + entry + s[idx:]
    io.open(path, "w", encoding="utf-8-sig").write(s)

def upsert_cs(path, keys):
    s = io.open(path, encoding="utf-8-sig").read()
    missing = [k for k in keys if f" {k} =>" not in s]
    if not missing:
        return
    block = "\n    // Added by tools/add_strings.py\n" + "".join(
        f"    public static string {k} => Get(nameof({k}));\n" for k in missing)
    idx = s.rstrip().rindex("}")
    s = s[:idx].rstrip() + "\n" + block + "}\n"
    io.open(path, "w", encoding="utf-8-sig").write(s)

def main():
    data = json.load(io.open(sys.argv[1], encoding="utf-8"))
    for key, (zh, en) in data.items():
        if not re.fullmatch(r"[A-Za-z][A-Za-z0-9]*", key):
            raise SystemExit(f"不合法的 key：{key!r}")
        upsert_resx(ROOT + "Strings.resx", key, zh)
        upsert_resx(ROOT + "Strings.en.resx", key, en)
    upsert_cs(ROOT + "Strings.cs", list(data))
    print(f"已處理 {len(data)} 個字串")

if __name__ == "__main__":
    main()
