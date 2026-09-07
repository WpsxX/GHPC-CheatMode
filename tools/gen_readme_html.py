# -*- coding: utf-8 -*-
"""Rebuild the bilingual viewer docs/README.html from README.md + README.zh-CN.md.

Usage (after editing either README file):
    python tools/gen_readme_html.py

The viewer embeds the rendered EN and zh-CN READMEs as two <section> blocks and
switches between them with JavaScript. Reading position is preserved *by heading
index* (both language versions share the same heading order), so toggling
languages lands you on the matching section instead of resetting to the top.
"""
import re
import pathlib
import markdown

ROOT = pathlib.Path(__file__).resolve().parents[1]   # CheatMode-Release/
EN_MD = ROOT / "README.md"
ZH_MD = ROOT / "README.zh-CN.md"
OUT = ROOT / "docs" / "README.html"

MD_EXT = ["tables", "fenced_code", "sane_lists", "nl2br"]


def strip_toc(md_text: str) -> str:
    """Remove the markdown 'Table of Contents' block; the viewer rebuilds a
    clickable TOC in JS from the rendered headings instead."""
    pat = re.compile(
        r"^## (Table of Contents|目录)\s*\n.*?(?=^---\s*$)", re.M | re.S)
    out, n = pat.subn("", md_text)
    return out if n else md_text


def rewrite_relative_links(html: str) -> str:
    """docs/README.html lives one level below the repo root: fix relative links
    that were written for the root-level README.md files."""
    html = html.replace('href="docs/COMPARISON.en.md"',
                        'href="COMPARISON.en.md"')
    html = html.replace('href="docs/COMPARISON.md"',
                        'href="COMPARISON.md"')
    html = html.replace('href="LICENSE"', 'href="../LICENSE"')
    return html


def render(md_path: pathlib.Path) -> str:
    text = strip_toc(md_path.read_text(encoding="utf-8"))
    body = markdown.markdown(text, extensions=MD_EXT)
    return rewrite_relative_links(body)


en_html = render(EN_MD)
zh_html = render(ZH_MD)

PAGE = """<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>CheatMode for Gunner, HEAT, PC! — README</title>
<style>
  :root {
    --bg: #ffffff; --fg: #1f2328; --muted: #59636e;
    --accent: #0969da; --border: #d1d9e0; --code-bg: #f6f8fa;
    --bar-bg: #f6f8fa; --hover: #eaeef2;
  }
  * { box-sizing: border-box; }
  html { scroll-behavior: smooth; }
  body {
    margin: 0; background: var(--bg); color: var(--fg);
    font: 15px/1.7 -apple-system, BlinkMacSystemFont, "Segoe UI", "PingFang SC",
          "Hiragino Sans GB", "Microsoft YaHei", Roboto, Helvetica, Arial, sans-serif;
  }
  /* ---- top language bar (sticky) ---- */
  .langbar {
    position: sticky; top: 0; z-index: 50;
    display: flex; align-items: center; gap: 8px;
    padding: 8px 20px; background: var(--bar-bg);
    border-bottom: 1px solid var(--border);
  }
  .langbar .brand { font-weight: 600; margin-right: auto; font-size: 14px; }
  .langbar button {
    border: 1px solid var(--border); background: var(--bg); color: var(--fg);
    padding: 5px 16px; border-radius: 6px; cursor: pointer; font-size: 13px;
  }
  .langbar button:hover { background: var(--hover); }
  .langbar button.active {
    background: var(--accent); border-color: var(--accent); color: #fff;
  }
  .langbar .hint { color: var(--muted); font-size: 12px; margin-left: 6px; }

  /* ---- layout ---- */
  .wrap { display: flex; max-width: 1100px; margin: 0 auto; }
  nav.toc {
    flex: 0 0 240px; padding: 20px 16px 40px 20px; font-size: 13px;
    position: sticky; top: 57px; align-self: flex-start; max-height: calc(100vh - 70px);
    overflow: auto;
  }
  nav.toc a {
    display: block; color: var(--muted); text-decoration: none;
    padding: 3px 0; border-left: 2px solid transparent; padding-left: 8px;
  }
  nav.toc a:hover { color: var(--accent); }
  nav.toc a.l2 { padding-left: 18px; }
  nav.toc a.active { color: var(--accent); border-left-color: var(--accent); }
  main.doc { flex: 1 1 auto; min-width: 0; padding: 20px 32px 80px 24px; }

  /* ---- markdown typography ---- */
  main.doc h1 { font-size: 30px; border-bottom: 1px solid var(--border); padding-bottom: .3em; margin: 0 0 .6em; }
  main.doc h2 { font-size: 22px; margin: 1.6em 0 .5em; border-bottom: 1px solid var(--border); padding-bottom: .25em; }
  main.doc h3 { font-size: 17px; margin: 1.3em 0 .4em; }
  main.doc p, main.doc li { line-height: 1.75; }
  main.doc code {
    background: var(--code-bg); padding: .15em .4em; border-radius: 4px;
    font: 12.5px "SFMono-Regular", Consolas, "Liberation Mono", Menlo, monospace;
  }
  main.doc pre {
    background: var(--code-bg); padding: 12px 14px; border-radius: 6px;
    overflow-x: auto; font: 12.5px "SFMono-Regular", Consolas, Menlo, monospace;
  }
  main.doc pre code { background: none; padding: 0; }
  main.doc blockquote {
    margin: 1em 0; padding: .5em 1em; color: var(--muted);
    border-left: 4px solid var(--border); background: var(--code-bg); border-radius: 0 6px 6px 0;
  }
  main.doc table {
    border-collapse: collapse; width: 100%; margin: 1em 0; display: block; overflow-x: auto;
  }
  main.doc th, main.doc td { border: 1px solid var(--border); padding: 7px 12px; text-align: left; font-size: 14px; }
  main.doc th { background: var(--bar-bg); }
  main.doc a { color: var(--accent); text-decoration: none; }
  main.doc a:hover { text-decoration: underline; }
  main.doc hr { border: 0; border-top: 1px solid var(--border); margin: 2em 0; }
  main.doc img { max-width: 100%; }

  @media (max-width: 820px) {
    nav.toc { display: none; }
    main.doc { padding: 16px 14px 80px; }
    .langbar .hint { display: none; }
  }
</style>
</head>
<body>

<div class="langbar" role="toolbar" aria-label="Language">
  <span class="brand">CheatMode README</span>
  <button id="btn-en" class="active" data-lang="en">English</button>
  <button id="btn-zh" data-lang="zh">中文</button>
  <span class="hint" id="pos-hint"></span>
</div>

<div class="wrap">
  <nav class="toc" id="toc" aria-label="Table of contents"></nav>
  <main class="doc">
    <!-- EN (rendered from README.md) -->
    <section id="view-en" lang="en" data-hidx="0">__EN_HTML__</section>
    <!-- zh-CN (rendered from README.zh-CN.md) -->
    <section id="view-zh" lang="zh" hidden data-hidx="0">__ZH_HTML__</section>
  </main>
</div>

<script>
(function () {
  "use strict";
  var LANGS = ["en", "zh"];
  var current = "en";

  // ---- index headings inside both sections (shared heading order) ----
  var sections = {};
  LANGS.forEach(function (lg) {
    var sec = document.getElementById("view-" + lg);
    var idx = 0;
    var heads = sec.querySelectorAll("h1, h2, h3");
    heads.forEach(function (h) {
      h.setAttribute("data-hidx", String(idx));
      idx++;
    });
    sections[lg] = sec;
  });

  // ---- build TOC from the ACTIVE language's headings ----
  function buildToc(lg) {
    var toc = document.getElementById("toc");
    toc.innerHTML = "";
    var heads = sections[lg].querySelectorAll("h2, h3");
    heads.forEach(function (h) {
      var hidx = h.getAttribute("data-hidx");
      var a = document.createElement("a");
      a.textContent = h.textContent;
      a.href = "#hd-" + lg + "-" + hidx;
      a.className = h.tagName === "H3" ? "l2" : "";
      h.setAttribute("id", "hd-" + lg + "-" + hidx);
      toc.appendChild(a);
    });
  }

  // ---- show one language, hide the other ----
  function showLang(lg) {
    LANGS.forEach(function (l) {
      sections[l].hidden = (l !== lg);
    });
    document.getElementById("btn-en").classList.toggle("active", lg === "en");
    document.getElementById("btn-zh").classList.toggle("active", lg === "zh");
    current = lg;
    document.documentElement.lang = lg === "zh" ? "zh-CN" : "en";
    buildToc(lg);
  }

  // ---- position helpers ----
  function headingForOffset(lg, offsetY) {
    // return the data-hidx of the heading nearest above the given scroll offset
    var heads = sections[lg].querySelectorAll("h2, h3");
    var chosen = null;
    for (var i = 0; i < heads.length; i++) {
      if (heads[i].getBoundingClientRect().top + window.scrollY <= offsetY + 60) {
        chosen = heads[i].getAttribute("data-hidx");
      } else {
        break;
      }
    }
    return chosen;
  }

  function scrollToHidx(lg, hidx) {
    if (hidx === null || hidx === undefined) { window.scrollTo(0, 0); return; }
    var heads = sections[lg].querySelectorAll("[data-hidx='" + hidx + "']");
    if (heads.length === 0) { window.scrollTo(0, 0); return; }
    var el = heads[0];
    var y = el.getBoundingClientRect().top + window.scrollY - 70;
    window.scrollTo(0, Math.max(0, y));
  }

  function switchTo(lg) {
    if (lg === current) return;
    // remember where we were reading (by heading index)
    var prevPos = headingForOffset(current, window.scrollY);
    showLang(lg);
    // restore the same reading position in the new language
    scrollToHidx(lg, prevPos);
  }

  document.getElementById("btn-en").addEventListener("click", function () { switchTo("en"); });
  document.getElementById("btn-zh").addEventListener("click", function () { switchTo("zh"); });

  // TOC click -> scroll to that heading inside the current language
  document.getElementById("toc").addEventListener("click", function (e) {
    var t = e.target;
    if (t.tagName === "A") {
      var id = t.getAttribute("href").slice(1);          // hd-<lang>-<idx>
      var m = id.match(/^hd-(en|zh)-(.+)$/);
      if (m) {
        e.preventDefault();
        scrollToHidx(current, m[2]);
        history.replaceState(null, "", "#" + id);
      }
    }
  });

  // highlight active TOC entry while scrolling
  var tocLinks = document.getElementById("toc");
  window.addEventListener("scroll", function () {
    var heads = sections[current].querySelectorAll("h2, h3");
    var cur = null;
    for (var i = 0; i < heads.length; i++) {
      if (heads[i].getBoundingClientRect().top <= 90) cur = heads[i].getAttribute("data-hidx");
    }
    tocLinks.querySelectorAll("a").forEach(function (a) {
      a.classList.toggle("active", a.getAttribute("href") === "#hd-" + current + "-" + cur);
    });
  }, { passive: true });

  showLang("en");
})();
</script>
</body>
</html>
"""

PAGE = PAGE.replace("__EN_HTML__", en_html).replace("__ZH_HTML__", zh_html)
OUT.write_text(PAGE, encoding="utf-8")
print("written:", OUT)
print("size:", OUT.stat().st_size, "bytes")
