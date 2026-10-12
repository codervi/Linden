import { get, post, setUnauthorizedHandler } from "./api.js";
import { h } from "./dom.js";
import { renderLogin } from "./views/login.js";
import * as dashboard from "./views/dashboard.js";
import * as clients from "./views/clients.js";
import * as clientDetail from "./views/clientDetail.js";
import * as schedule from "./views/schedule.js";
import * as audit from "./views/audit.js";

const root = document.getElementById("app");
let me = null;
let view = null;
let navLinks = {};
let routeId = 0;

const routes = [
  { pattern: /^#?\/?$/, nav: "dashboard", view: dashboard },
  { pattern: /^#\/dashboard$/, nav: "dashboard", view: dashboard },
  { pattern: /^#\/clients$/, nav: "clients", view: clients },
  { pattern: /^#\/clients\/(\d+)$/, nav: "clients", view: clientDetail },
  { pattern: /^#\/schedule$/, nav: "schedule", view: schedule },
  { pattern: /^#\/audit$/, nav: "audit", view: audit, adminOnly: true },
];

// If any request comes back 401 (session expired), go back to the login screen.
setUnauthorizedHandler(() => {
  if (me) {
    me = null;
    showLogin("Your session ended. Please sign in again.");
  }
});

function showLogin(message = "") {
  renderLogin(root, (user) => { me = user; showShell(); }, message);
}

function showShell() {
  const links = [
    ["dashboard", "#/dashboard", "Dashboard"],
    ["clients", "#/clients", "Clients"],
    ["schedule", "#/schedule", "Schedule"],
  ];
  if (me.role === "Admin") links.push(["audit", "#/audit", "Audit log"]);

  navLinks = {};
  const nav = h("nav", { "aria-label": "Main" }, links.map(([key, href, label]) => {
    const a = h("a", { href }, label);
    navLinks[key] = a;
    return a;
  }));

  view = h("main", { id: "view", tabindex: "-1" });

  root.replaceChildren(
    h("header", { class: "topbar" },
      h("div", { class: "topbar-inner" },
        h("span", { class: "brand" }, "Linden"),
        nav,
        h("div", { class: "who" },
          h("span", {}, me.email, " ", h("span", { class: "role" }, me.role === "Admin" ? "Psychologist" : "Reception")),
          h("button", { class: "btn btn-ghost btn-small", onclick: signOut }, "Sign out")))),
    view);

  if (!location.hash) location.hash = "#/dashboard";
  route();
}

async function signOut() {
  try { await post("/auth/logout"); } catch { /* already signed out */ }
  me = null;
  location.hash = "";
  showLogin();
}

async function route() {
  if (!me || !view) return;
  const mine = ++routeId;

  const hash = location.hash;
  const match = routes.map((r) => ({ r, m: hash.match(r.pattern) })).find((x) => x.m);

  for (const [key, link] of Object.entries(navLinks)) {
    if (match && key === match.r.nav) link.setAttribute("aria-current", "page");
    else link.removeAttribute("aria-current");
  }

  // Each screen draws into its own container, so a slow screen
  // that finishes late can't overwrite the one the user moved to.
  const container = h("div");
  view.replaceChildren(container);

  if (!match) {
    container.append(h("h1", {}, "Page not found"), h("a", { href: "#/dashboard" }, "Go to the dashboard"));
    return;
  }
  if (match.r.adminOnly && me.role !== "Admin") {
    container.append(h("h1", {}, "Not allowed"), h("p", {}, "Only the psychologist can open this page."));
    return;
  }

  container.append(h("p", { class: "muted" }, "Loading..."));
  try {
    await match.r.view.render(container, { me, params: match.m.slice(1) });
    if (mine === routeId) view.focus({ preventScroll: true });
  } catch (err) {
    if (mine !== routeId || !me) return;
    container.replaceChildren(
      h("h1", {}, err.status === 404 ? "Not found" : "Something went wrong"),
      h("p", {}, err.status === 404 ? "That record does not exist." : err.message),
      h("a", { href: "#/dashboard" }, "Go to the dashboard"));
  }
}

window.addEventListener("hashchange", route);

(async function start() {
  try {
    me = await get("/auth/me");
    showShell();
  } catch {
    showLogin();
  }
})();
