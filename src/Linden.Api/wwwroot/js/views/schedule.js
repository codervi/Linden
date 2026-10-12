import { get } from "../api.js";
import { h, badge, emptyState, fmtDay, fmtTime, isoDate, addDays, mondayOf } from "../dom.js";
import { appointmentDialog } from "../forms.js";

export async function render(view) {
  let weekStart = mondayOf(new Date());
  let requestId = 0;

  const title = h("h2", { class: "week-title" });
  const grid = h("div", { class: "week" });

  async function load() {
    const mine = ++requestId;
    const list = await get(`/appointments?from=${isoDate(weekStart)}&to=${isoDate(addDays(weekStart, 6))}`);
    if (mine !== requestId) return;

    title.textContent = `${fmtDay(weekStart)} to ${fmtDay(addDays(weekStart, 4))}`;
    grid.replaceChildren(...[0, 1, 2, 3, 4].map((i) => {
      const day = addDays(weekStart, i);
      const key = isoDate(day);
      return column(day, list.filter((a) => a.startsAt.slice(0, 10) === key));
    }));
  }

  function column(day, items) {
    const key = isoDate(day);
    const isToday = key === isoDate(new Date());
    const active = items.filter((a) => a.status === "Scheduled" || a.status === "Completed");

    return h("section", { class: "day" + (isToday ? " day-today" : "") },
      h("header", {},
        h("h3", {}, fmtDay(day)),
        h("button", {
          class: "icon-btn",
          "aria-label": `Book an appointment on ${fmtDay(day)}`,
          onclick: () => appointmentDialog({ date: key, onSaved: load }),
        }, "+")),
      active.length
        ? h("ul", {}, active.map((a) =>
            h("li", {},
              h("span", { class: "slot-time" }, `${fmtTime(a.startsAt)} to ${fmtTime(a.endsAt)}`),
              h("a", { href: `#/clients/${a.clientId}` }, a.clientName),
              a.status === "Scheduled" ? null : badge(a.status))))
        : h("p", { class: "muted" }, "Free"));
  }

  function shift(weeks) {
    weekStart = addDays(weekStart, weeks * 7);
    load();
  }

  view.replaceChildren(
    h("div", { class: "page-head" },
      h("h1", {}, "Schedule"),
      h("button", { class: "btn", onclick: () => appointmentDialog({ onSaved: load }) }, "Book appointment")),
    h("div", { class: "toolbar" },
      h("button", { class: "btn btn-ghost btn-small", onclick: () => shift(-1) }, "Previous week"),
      h("button", { class: "btn btn-ghost btn-small", onclick: () => { weekStart = mondayOf(new Date()); load(); } }, "This week"),
      h("button", { class: "btn btn-ghost btn-small", onclick: () => shift(1) }, "Next week"),
      title),
    grid);

  await load();
}
