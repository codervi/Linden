// Small helpers for building the page.
// Everything is created with createElement and text nodes. Nothing that comes
// from the server is ever put into innerHTML, so client names or notes can't inject markup.

export function h(tag, attrs, ...children) {
  const el = document.createElement(tag);
  let value;
  for (const [key, val] of Object.entries(attrs ?? {})) {
    if (val === null || val === undefined || val === false) continue;
    if (key === "class") el.className = val;
    else if (key === "value") value = val;
    else if (key.startsWith("on") && typeof val === "function") el.addEventListener(key.slice(2), val);
    else el.setAttribute(key, val === true ? "" : String(val));
  }
  for (const child of children.flat(Infinity)) {
    if (child === null || child === undefined || child === false) continue;
    el.append(child instanceof Node ? child : document.createTextNode(String(child)));
  }
  // value is set last so <select> already has its options
  if (value !== undefined) el.value = value;
  return el;
}

let nextId = 0;
export function field(label, input, hint) {
  const id = "field-" + ++nextId;
  input.id = id;
  return h("div", { class: "field" },
    h("label", { for: id }, label),
    input,
    hint ? h("p", { class: "hint" }, hint) : null);
}

export function toast(message, kind = "ok") {
  const box = document.getElementById("toasts");
  const el = h("div", { class: "toast toast-" + kind }, message);
  box.append(el);
  setTimeout(() => el.remove(), 4000);
}

export function modal(title, build) {
  const dialog = h("dialog", { class: "modal" });
  const close = () => dialog.close();
  dialog.addEventListener("close", () => dialog.remove());
  dialog.append(
    h("div", { class: "modal-head" },
      h("h2", {}, title),
      h("button", { type: "button", class: "icon-btn", "aria-label": "Close", onclick: close }, "\u00d7")),
    build(close));
  document.body.append(dialog);
  dialog.showModal();
  return close;
}

export function badge(status) {
  return h("span", { class: "badge badge-" + String(status).toLowerCase() }, status === "NoShow" ? "No-show" : status);
}

export function emptyState(text) {
  return h("p", { class: "empty" }, text);
}

export function pager(page, totalPages, go) {
  if (totalPages <= 1) return h("div");
  return h("nav", { class: "pager", "aria-label": "Pages" },
    h("button", { class: "btn btn-ghost btn-small", disabled: page <= 1, onclick: () => go(page - 1) }, "Previous"),
    h("span", {}, `Page ${page} of ${totalPages}`),
    h("button", { class: "btn btn-ghost btn-small", disabled: page >= totalPages, onclick: () => go(page + 1) }, "Next"));
}

export function debounce(fn, ms = 300) {
  let timer;
  return (...args) => {
    clearTimeout(timer);
    timer = setTimeout(() => fn(...args), ms);
  };
}

// ---- dates ----
// Appointment times are practice-local with no offset ("2026-10-14T10:00:00"),
// and the browser reads that as local time, which is what we want.
// "...Utc" fields come back without a Z from SQL Server, so one is added.

const dayFmt = new Intl.DateTimeFormat("en-GB", { weekday: "short", day: "numeric", month: "short" });
const dateFmt = new Intl.DateTimeFormat("en-GB", { weekday: "short", day: "numeric", month: "short", year: "numeric" });
const timeFmt = new Intl.DateTimeFormat("en-GB", { hour: "2-digit", minute: "2-digit" });
const stampFmt = new Intl.DateTimeFormat("en-GB", { dateStyle: "medium", timeStyle: "short" });
const dobFmt = new Intl.DateTimeFormat("en-GB", { day: "numeric", month: "short", year: "numeric" });

export const fmtDay = (d) => dayFmt.format(d instanceof Date ? d : new Date(d));
export const fmtDate = (s) => dateFmt.format(new Date(s));
export const fmtTime = (s) => timeFmt.format(new Date(s));

export function parseUtc(s) {
  return new Date(/(Z|[+-]\d\d:\d\d)$/.test(s) ? s : s + "Z");
}
export const fmtStamp = (s) => stampFmt.format(parseUtc(s));

function parseDateOnly(s) {
  const [y, m, d] = s.split("-").map(Number);
  return new Date(y, m - 1, d);
}
export const fmtDob = (s) => (s ? dobFmt.format(parseDateOnly(s)) : "");

export function ageFrom(s) {
  if (!s) return null;
  const born = parseDateOnly(s);
  const now = new Date();
  let age = now.getFullYear() - born.getFullYear();
  const birthdayPassed = now.getMonth() > born.getMonth()
    || (now.getMonth() === born.getMonth() && now.getDate() >= born.getDate());
  if (!birthdayPassed) age--;
  return age;
}

const pad = (n) => String(n).padStart(2, "0");
export const isoDate = (d) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;

export function addDays(d, n) {
  const x = new Date(d.getFullYear(), d.getMonth(), d.getDate());
  x.setDate(x.getDate() + n);
  return x;
}
export function mondayOf(d) {
  return addDays(d, -((d.getDay() + 6) % 7));
}
