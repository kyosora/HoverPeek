"""把翻譯 JSON 轉成 Strings.{語言}.resx，先驗證再寫入。

用法：python tools/build_locale_resx.py <語言代碼> <翻譯.json>
例如：python tools/build_locale_resx.py ja .ai/i18n/ja.json

翻譯 JSON 是平的 {"Key": "譯文"}。以 Strings.resx（繁中原文）為準，以下任一不符就整批拒絕、不寫檔：
- key 集合必須完全相同（不多不少）
- 每個 key 的 {0}、{1}… 佔位符集合要和原文相同（少了會在 string.Format 時崩潰）
- 換行數要和原文相同
- 不能是空字串
"""
import io
import json
import re
import sys
from html import unescape
from xml.sax.saxutils import escape

ROOT = "src/HoverPeek.Core/Localization/"
PLACEHOLDER = re.compile(r"\{\d+\}")


def load_resx(path):
    s = io.open(path, encoding="utf-8-sig").read()
    pairs = re.findall(r'<data name="([^"]+)"[^>]*>\s*<value>(.*?)</value>', s, re.S)
    return s, {k: unescape(v) for k, v in pairs}


def validate(source, translated):
    errors = []
    missing = sorted(set(source) - set(translated))
    extra = sorted(set(translated) - set(source))
    if missing:
        errors.append(f"缺少 {len(missing)} 個 key：{', '.join(missing[:10])}")
    if extra:
        errors.append(f"多出 {len(extra)} 個 key：{', '.join(extra[:10])}")

    for key in sorted(set(source) & set(translated)):
        src, dst = source[key], translated[key]
        if not isinstance(dst, str) or not dst.strip():
            errors.append(f"{key}：空字串")
            continue
        if sorted(PLACEHOLDER.findall(src)) != sorted(PLACEHOLDER.findall(dst)):
            errors.append(f"{key}：佔位符不符 原文={PLACEHOLDER.findall(src)} 譯文={PLACEHOLDER.findall(dst)}")
        if src.count("\n") != dst.count("\n"):
            errors.append(f"{key}：換行數不符 原文={src.count(chr(10))} 譯文={dst.count(chr(10))}")
    return errors


def main():
    if len(sys.argv) != 3:
        raise SystemExit(__doc__)
    code, json_path = sys.argv[1], sys.argv[2]
    if not re.fullmatch(r"[a-z]{2}(-[A-Z]{2})?", code):
        raise SystemExit(f"語言代碼格式不對：{code!r}（例如 ja、zh-CN）")

    template, source = load_resx(ROOT + "Strings.resx")
    translated = json.load(io.open(json_path, encoding="utf-8-sig"))

    errors = validate(source, translated)
    if errors:
        print(f"{code}：驗證失敗，沒有寫入任何檔案", file=sys.stderr)
        for e in errors:
            print("  - " + e, file=sys.stderr)
        raise SystemExit(1)

    # 沿用 Strings.resx 的檔頭（schema、resheader），只換掉 <data> 內容
    head = template[: template.index("<data ")]
    body = "".join(
        f'  <data name="{k}" xml:space="preserve">\n    <value>{escape(translated[k])}</value>\n  </data>\n'
        for k in source
    )
    out = ROOT + f"Strings.{code}.resx"
    io.open(out, "w", encoding="utf-8-sig").write(head + body + "</root>\n")
    print(f"{code}：{len(source)} 個字串已驗證並寫入 {out}")


if __name__ == "__main__":
    main()
