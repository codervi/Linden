import { get } from "../api.js";
import { h, badge, emptyState, fmtTime, fmtDate } from "../dom.js";

export async function render(view) {
  const d = await get("/dashboard");

  const list = d.today.length
    ? h("ul", { class: "agenda" }, d.today.map((a) =>
        h("li", {},
          h("span", { class: "agenda-time" }, `${fmtTime(a.startsAt)} to ${fmtTime(a.endsAt)}`),
          h("a", { href: `#/clients/${a.clientId}` }, a.clientName),
          badge(a.status))))
    : emptyState("No appointments scheduled for today.");

  view.replaceChildren(
    h("h1", {}, "Today"),
    h("p", { class: "muted" }, fmtDate(new Date())),
    h("div", { class: "stats" },
      stat(d.todayCount, "appointments today"),
      stat(d.weekCount, "in the next 7 days"),
      stat(d.activeClients, "active clients", "#/clients")),
    h("h2", {}, "Today's appointments"),
    list);
}

function stat(number, label, href) {
  const inner = [h("span", { class: "stat-n" }, number), label];
  return href ? h("a", { class: "stat", href }, inner) : h("div", { class: "stat" }, inner);
}
