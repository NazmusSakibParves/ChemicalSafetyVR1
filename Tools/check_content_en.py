"""Checks Assets/Resources/Content/*.json against the English the game shows today.

    python Tools/check_content_en.py            # compare with the C# at git HEAD
    python Tools/check_content_en.py --rev <git revision that still has the original string constants>

Checks:
  * SC03..SC10: every string const / string[] of ScNNContent.cs at the revision must exist in ScNN.json with a
    byte-identical `en` (and the JSON must not have extra keys). C# escapes (\\n, \\", \\u...) and "a" + "b"
    concatenations are resolved the same way the compiler does.
  * SC01, SC02 (no content class: texts are inline in PresentableSequenceDirector.cs) and GENERAL: each `en` must be
    a string literal (or a "a" + "b" chain of literals) that really exists in the runtime scripts.
  * Every file: keys unique, `en` and `bn` non-empty, arrays have the same length in en and bn; inside an array an
    empty element is allowed only when it is empty in BOTH languages (the "no red title for the correct option" slot).
Exit code 0 = no differences.
"""
import argparse
import glob
import json
import os
import subprocess
import sys

sys.stdout.reconfigure(encoding="utf-8")

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONTENT_DIR = os.path.join(ROOT, "Assets", "Resources", "Content")
PRES = "Assets/Scripts/ChemicalSafety/Presentable"
DIRECTOR = PRES + "/PresentableSequenceDirector.cs"
BUILDER = "Assets/Scripts/ChemicalSafety/Editor/PresentableSceneBuilder.cs"
CONTENT_SCENARIOS = ["SC03", "SC04", "SC05", "SC06", "SC07", "SC08", "SC09", "SC10"]
INLINE_SCENARIOS = ["SC01", "SC02"]

ESC = {"n": "\n", "t": "\t", "r": "\r", "0": "\0", "\\": "\\", '"': '"', "'": "'", "a": "\a", "b": "\b", "f": "\f", "v": "\v"}


# ----------------------------------------------------------------------------------------------------- C# tokenizer
def tokenize(src):
    """Returns tokens: ('str', value) | ('id', name) | ('p', punctuation). Comments are dropped."""
    toks = []
    i, n = 0, len(src)
    while i < n:
        c = src[i]
        if c.isspace():
            i += 1
        elif src.startswith("//", i):
            j = src.find("\n", i)
            i = n if j < 0 else j
        elif src.startswith("/*", i):
            j = src.find("*/", i + 2)
            i = n if j < 0 else j + 2
        elif c == '"' or (c in "@$" and src[i + 1:i + 2] == '"') or (src[i:i + 2] in ("$@", "@$") and src[i + 2:i + 3] == '"'):
            verbatim = "@" in src[i:i + 2]
            j = i
            while src[j] != '"':
                j += 1
            j += 1
            buf = []
            while j < n:
                ch = src[j]
                if verbatim:
                    if ch == '"':
                        if src[j + 1:j + 2] == '"':
                            buf.append('"')
                            j += 2
                            continue
                        break
                    buf.append(ch)
                    j += 1
                else:
                    if ch == "\\":
                        e = src[j + 1]
                        if e == "u":
                            buf.append(chr(int(src[j + 2:j + 6], 16)))
                            j += 6
                        elif e == "x":
                            k = j + 2
                            while k < j + 6 and src[k] in "0123456789abcdefABCDEF":
                                k += 1
                            buf.append(chr(int(src[j + 2:k], 16)))
                            j = k
                        else:
                            buf.append(ESC.get(e, e))
                            j += 2
                    elif ch == '"':
                        break
                    else:
                        buf.append(ch)
                        j += 1
            toks.append(("str", "".join(buf)))
            i = j + 1
        elif c == "'":
            j = i + 1
            while src[j] != "'":
                j += 2 if src[j] == "\\" else 1
            toks.append(("chr", src[i:j + 1]))
            i = j + 1
        elif c.isalpha() or c == "_":
            j = i
            while j < n and (src[j].isalnum() or src[j] == "_"):
                j += 1
            toks.append(("id", src[i:j]))
            i = j
        elif src.startswith("=>", i):
            toks.append(("p", "=>"))
            i += 2
        else:
            toks.append(("p", c))
            i += 1
    return toks


def eval_concat(toks, consts):
    """tokens of  "a" + "b" + Name  ->  joined string (None when not a plain string expression)."""
    out = []
    expect_operand = True
    for kind, val in toks:
        if expect_operand:
            if kind == "str":
                out.append(val)
            elif kind == "id" and val in consts:
                out.append(consts[val])
            else:
                return None
            expect_operand = False
        else:
            if (kind, val) != ("p", "+"):
                return None
            expect_operand = True
    return None if expect_operand else "".join(out)


def parse_members(src):
    """Returns {name: str | list[str]} for every `const string` and `static readonly string[]` member, in source order."""
    toks = tokenize(src)
    consts, members = {}, {}
    i, n = 0, len(toks)
    while i < n:
        kind, val = toks[i]
        is_arr = False
        j = i
        if (kind, val) == ("id", "string"):
            if toks[i + 1:i + 3] == [("p", "["), ("p", "]")]:
                is_arr = True
                j = i + 3
            else:
                j = i + 1
            if j + 1 < n and toks[j][0] == "id" and toks[j + 1] == ("p", "="):
                prefix = {t[1] for t in toks[max(0, i - 4):i]}
                if (is_arr and "readonly" in prefix) or (not is_arr and "const" in prefix):
                    name = toks[j][1]
                    k = j + 2
                    depth, expr = 0, []
                    while k < n:
                        t = toks[k]
                        if t == ("p", "{"):
                            depth += 1
                        elif t == ("p", "}"):
                            depth -= 1
                        elif t == ("p", ";") and depth == 0:
                            break
                        expr.append(t)
                        k += 1
                    if is_arr:
                        inner = expr[1:-1] if expr and expr[0] == ("p", "{") else expr
                        items, cur = [], []
                        for t in inner:
                            if t == ("p", ","):
                                items.append(cur)
                                cur = []
                            else:
                                cur.append(t)
                        if cur:
                            items.append(cur)
                        values = [eval_concat(it, consts) for it in items]
                        if None in values:
                            raise ValueError("cannot evaluate string[] " + name)
                        members[name] = values
                    else:
                        value = eval_concat(expr, consts)
                        if value is None:
                            raise ValueError("cannot evaluate const " + name)
                        consts[name] = value
                        members[name] = value
                    i = k
        i += 1
    return members


def literals(src):
    """Every string literal and every "a" + "b" chain of literals in the source."""
    toks = tokenize(src)
    found = set()
    i, n = 0, len(toks)
    while i < n:
        if toks[i][0] == "str":
            parts = [toks[i][1]]
            found.add(toks[i][1])
            j = i
            while j + 2 < n and toks[j + 1] == ("p", "+") and toks[j + 2][0] == "str":
                parts.append(toks[j + 2][1])
                found.add(toks[j + 2][1])
                found.add("".join(parts))
                j += 2
        i += 1
    return found


# ------------------------------------------------------------------------------------------------------------ helpers
def git_show(rev, path):
    r = subprocess.run(["git", "show", "%s:%s" % (rev, path)], cwd=ROOT, capture_output=True)
    if r.returncode != 0:
        return None
    return r.stdout.decode("utf-8-sig")


def read_text(path):
    with open(path, encoding="utf-8-sig") as f:
        return f.read()


def load_json(name):
    with open(os.path.join(CONTENT_DIR, name + ".json"), encoding="utf-8") as f:
        return json.load(f)


def flat(doc):
    """{key: ('s', en, bn) | ('a', [en], [bn])} plus structural problems."""
    problems, out = [], {}
    for e in doc.get("strings", []):
        key = e.get("key")
        if key in out:
            problems.append("duplicate key " + key)
        out[key] = ("s", e.get("en"), e.get("bn"))
    for e in doc.get("arrays", []):
        key = e.get("key")
        if key in out:
            problems.append("duplicate key " + key)
        out[key] = ("a", e.get("en"), e.get("bn"))
    return out, problems


def check_structure(name, entries):
    errs = []
    for key, (kind, en, bn) in entries.items():
        where = "%s.%s" % (name, key)
        if kind == "s":
            if not isinstance(en, str) or not en.strip():
                errs.append(where + ": en is empty")
            if not isinstance(bn, str) or not bn.strip():
                errs.append(where + ": bn is empty")
        else:
            if not isinstance(en, list) or not isinstance(bn, list):
                errs.append(where + ": en/bn must both be arrays")
                continue
            if len(en) != len(bn):
                errs.append(where + ": array length en=%d bn=%d" % (len(en), len(bn)))
                continue
            if not en:
                errs.append(where + ": empty array")
            for i, (a, b) in enumerate(zip(en, bn)):
                if bool(a) != bool(b):
                    errs.append("%s[%d]: empty in only one language (en=%r bn=%r)" % (where, i, a, b))
                elif not a and not b:
                    pass  # "no title" slot, empty in both
    return errs


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--rev", default="HEAD")
    args = ap.parse_args()
    errors, checked = [], 0

    # SC03..SC10: compare with the C# constants at the revision
    for sc in CONTENT_SCENARIOS:
        cs_path = "%s/Sc%sContent.cs" % (PRES, sc[2:])
        src = git_show(args.rev, cs_path)
        if src is None:
            errors.append("%s: %s not found at %s" % (sc, cs_path, args.rev))
            continue
        members = parse_members(src)
        if not members:
            errors.append("%s: no string constants at %s (already converted? use --rev <older commit>)" % (sc, args.rev))
            continue
        entries, problems = flat(load_json(sc))
        errors += ["%s: %s" % (sc, p) for p in problems]
        errors += check_structure(sc, entries)
        for name, value in members.items():
            checked += 1
            kind = "a" if isinstance(value, list) else "s"
            if name not in entries:
                errors.append("%s.%s: missing in JSON" % (sc, name))
            elif entries[name][0] != kind:
                errors.append("%s.%s: JSON has the wrong kind (string vs array)" % (sc, name))
            elif entries[name][1] != value:
                errors.append("%s.%s: en differs\n    C#  : %r\n    JSON: %r" % (sc, name, value, entries[name][1]))
        for key in entries:
            if key not in members:
                errors.append("%s.%s: in JSON but not a string member of the C# class" % (sc, key))

    # SC01, SC02, GENERAL: every en must be a literal that exists in the runtime scripts
    pool = set()
    for rel in (DIRECTOR, BUILDER):
        text = git_show(args.rev, rel)
        if text is None:
            text = read_text(os.path.join(ROOT, rel))
        pool |= literals(text)
    for folder in ("Presentable", "UI"):
        for path in glob.glob(os.path.join(ROOT, "Assets", "Scripts", "ChemicalSafety", folder, "*.cs")):
            base = os.path.basename(path)
            if base.startswith("Sc01Content") or base.startswith("Sc02Content"):
                continue  # these read the JSON, they must not vouch for it
            pool |= literals(read_text(path))
    for sc in INLINE_SCENARIOS + ["GENERAL"]:
        entries, problems = flat(load_json(sc))
        errors += ["%s: %s" % (sc, p) for p in problems]
        errors += check_structure(sc, entries)
        for key, (kind, en, bn) in entries.items():
            for text in (en if kind == "a" else [en]):
                if not text:
                    continue
                checked += 1
                if text not in pool:
                    errors.append("%s.%s: en is not a literal found in the runtime scripts: %r" % (sc, key, text))

    # LOGIN: structure only
    entries, problems = flat(load_json("LOGIN"))
    errors += ["LOGIN: %s" % p for p in problems]
    errors += check_structure("LOGIN", entries)

    if errors:
        print("DIFFERENCES: %d" % len(errors))
        for e in errors:
            print(" -", e)
        sys.exit(1)
    print("OK: %d English texts identical to the game, 0 differences" % checked)


if __name__ == "__main__":
    main()
