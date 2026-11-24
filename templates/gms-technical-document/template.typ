// GMS technical document template. Contract: `render(body, title: none, subtitle: none)` receives the converted
// Markdown as content, plus the title and subtitle for the title page (none when there is no title).
// Colours and type follow gms.ca: moonstone headings, ultra-violet links with a coral underline,
// and sunshine-yellow / mint-green accents. Public Sans is the typeface. HTML output is styled by theme.css.
#let moonstone = rgb("#3D376A")
#let ultra-violet = rgb("#5F4B8B")
#let mint-green = rgb("#50D3B4")
#let sunshine-yellow = rgb("#F8C441")
#let coral = rgb("#F15E44")
#let warm-grey = rgb("#F6F5F2")
#let ink = rgb("#222222")
// Tallest a diagram may be: A4 height minus the top and bottom margins below, less room for a heading.
#let diagram-max-height = 29.7cm - 2.8cm - 2.6cm - 3.5cm

// Numbering style for each depth of ordered list. Deeper levels reuse the last style.
#let enum-numbering = ("1.", "a.", "i.")
// Wraps an ordered list at the given depth (1 for the top level) so the template numbers it, not the Markdown.
#let numbered(depth, body) = {
  set enum(numbering: enum-numbering.at(calc.min(depth, enum-numbering.len()) - 1))
  body
}

// Heading sizes and spacing for levels 1 to 6.
#let heading-style = (
  "1": (size: 22pt, weight: "light", above: 1.2em, below: 1em),
  "2": (size: 15pt, weight: "light", above: 1.6em, below: 0.8em),
  "3": (size: 12pt, weight: "bold", above: 1.3em, below: 0.6em),
  "4": (size: 10.5pt, weight: "semibold", above: 1.1em, below: 0.5em),
  "5": (size: 10pt, weight: "semibold", above: 1em, below: 0.4em),
  "6": (size: 10pt, weight: "semibold", above: 1em, below: 0.4em, style: "italic"),
)

// Anchor id for a heading in HTML output, built from its index among all headings.
// The heading counter cannot be used because numbering is off, so it never advances.
#let heading-id(loc) = "s" + str(query(heading).position(h => h.location() == loc))

#let brand-bar = grid(
  columns: (1fr, 1fr, 1fr),
  rect(width: 100%, height: 3pt, fill: sunshine-yellow),
  rect(width: 100%, height: 3pt, fill: mint-green),
  rect(width: 100%, height: 3pt, fill: ultra-violet),
)

#let cover-paged(title, subtitle) = page(header: none, footer: none, margin: (x: 2.5cm, top: 3cm, bottom: 2.5cm), {
  image("assets/gms-logo-black.svg", height: 1.6cm)
  v(1fr)
  rect(width: 3cm, height: 4pt, fill: sunshine-yellow)
  v(0.9cm)
  text(size: 34pt, weight: "light", fill: moonstone, title)
  if subtitle != none {
    v(0.7cm)
    text(size: 14pt, fill: ultra-violet, subtitle)
  }
  v(1fr)
  brand-bar
  v(0.4em)
  text(size: 9pt, fill: moonstone)[Group Medical Services · gms.ca]
})

#let cover-html(title, subtitle) = html.elem("div", attrs: (class: "cover page"), {
  html.elem("div", attrs: (class: "cover-logo"), html.frame(image("assets/gms-logo-black.svg", height: 1.6cm)))
  html.elem("div", attrs: (class: "cover-main"), {
    html.elem("div", attrs: (class: "cover-rule"), [])
    html.elem("h1", attrs: (class: "cover-title"), title)
    if subtitle != none {
      html.elem("div", attrs: (class: "cover-subtitle"), subtitle)
    }
  })
  html.elem("div", attrs: (class: "cover-bar"), {
    html.elem("span", attrs: (class: "bar-y"), [])
    html.elem("span", attrs: (class: "bar-m"), [])
    html.elem("span", attrs: (class: "bar-v"), [])
  })
  html.elem("div", attrs: (class: "cover-footer"), [Group Medical Services · gms.ca])
})

#let cover(title, subtitle) = context if target() == "html" {
  cover-html(title, subtitle)
} else {
  cover-paged(title, subtitle)
}

#let toc-paged() = {
  text(size: 22pt, weight: "light", fill: moonstone)[Contents]
  v(0.35em)
  rect(width: 2.5cm, height: 3pt, fill: sunshine-yellow)
  v(1em)
  show outline.entry.where(level: 1): it => {
    v(0.9em, weak: true)
    text(weight: "bold", fill: moonstone, it)
  }
  outline(title: none, depth: 3, indent: 1.4em)
}

#let toc-html() = html.elem("div", attrs: (class: "toc page"), {
  html.elem("h2", attrs: (class: "toc-title"), [Contents])
  html.elem("ul", attrs: (class: "toc-list"), {
    for h in query(heading).filter(h => h.level <= 3) {
      html.elem("li", attrs: (class: "toc-l" + str(h.level)),
        html.elem("a", attrs: (href: "#" + heading-id(h.location())), h.body))
    }
  })
})

// The contents page is on its own page in PDF output. HTML gets a linked list of the same headings.
#let toc() = context if target() == "html" {
  toc-html()
} else {
  pagebreak(weak: true)
  toc-paged()
  pagebreak()
}

#let render(body, title: none, subtitle: none) = {
  set text(font: "Public Sans", size: 10.5pt, fill: ink, lang: "en")
  set par(justify: false, leading: 0.8em, spacing: 1.3em)
  set heading(numbering: none)
  set page(
    paper: "a4",
    margin: (x: 2cm, top: 2.8cm, bottom: 2.6cm),
    header: {
      grid(
        columns: (1fr, auto),
        align: (left + horizon, right + horizon),
        image("assets/gms-logo-black.svg", height: 0.8cm),
        text(size: 8pt, fill: ultra-violet)[Group Medical Services],
      )
      v(0.3em)
      line(length: 100%, stroke: 0.6pt + mint-green)
    },
    footer: {
      brand-bar
      v(0.4em)
      grid(
        columns: (1fr, auto),
        text(size: 8pt, fill: moonstone)[gms.ca],
        context text(size: 8pt, fill: moonstone, counter(page).display("1 / 1", both: true)),
      )
    },
  )

  // In HTML the heading keeps its real <hN> tag and id (used by the contents links); CSS styles it.
  show heading: it => context if target() == "html" {
    html.elem("h" + str(it.level), attrs: (id: heading-id(it.location())), it.body)
  } else {
    let s = heading-style.at(str(it.level))
    block(above: s.above, below: s.below, sticky: true)[
      #text(size: s.size, weight: s.weight, style: s.at("style", default: "normal"), fill: moonstone, it.body)
      #if it.level == 1 [ #v(0.35em) #rect(width: 2.5cm, height: 3pt, fill: sunshine-yellow) ]
    ]
  }

  show link: it => underline(offset: 2.5pt, stroke: 0.8pt + coral, text(fill: ultra-violet, it))

  // One bullet per depth: level 1 is a dot, level 2 a dash, level 3 and deeper a square.
  set list(marker: (
    text(fill: mint-green, weight: "bold")[•],
    text(fill: ultra-violet, weight: "bold")[–],
    text(fill: moonstone, weight: "bold")[▪],
  ))
  // No outer box: only inner column lines, plus a bold rule under the header row.
  set table(
    stroke: (x, y) => (
      left: if x > 0 { 0.5pt + rgb("#e3e1db") } else { none },
      right: none,
      top: if y == 0 { none } else if y == 1 { 1.5pt + moonstone } else { 0.5pt + rgb("#e3e1db") },
      bottom: none,
    ),
    inset: 6pt,
  )
  show table.cell.where(y: 0): set text(weight: "bold", fill: moonstone)
  show table: it => context if target() == "html" { it } else { align(center, it) }

  show quote.where(block: true): it => context if target() == "html" {
    html.elem("blockquote", it.body)
  } else {
    block(
      width: 100%,
      fill: warm-grey,
      inset: (left: 12pt, right: 9pt, y: 8pt),
      radius: 3pt,
      stroke: (left: 2.5pt + sunshine-yellow),
      above: 1em,
      below: 1em,
      text(fill: moonstone, it.body),
    )
  }

  show raw: set text(font: "DejaVu Sans Mono", size: 0.9em)
  show raw.where(block: false): it => box(fill: warm-grey, inset: (x: 3pt, y: 0pt), outset: (y: 2pt), radius: 2pt, it)
  show raw.where(block: true): it => block(
    width: 100%,
    fill: warm-grey,
    inset: 9pt,
    radius: 3pt,
    stroke: (left: 2.5pt + mint-green),
    above: 1em,
    below: 1em,
    it,
  )

  // Typst's HTML export ignores page setup, so the brand font is loaded from Google Fonts and the
  // theme stylesheet supplies the rest of the look.
  context if target() == "html" {
    html.elem("link", attrs: (
      rel: "stylesheet",
      href: "https://fonts.googleapis.com/css2?family=Public+Sans:wght@300;400;600;700&display=swap",
    ))
    html.elem("style", read("theme.css"))
  }

  if title != none {
    cover(title, subtitle)
  }
  context if query(heading).len() > 0 {
    toc()
  }

  body
}
