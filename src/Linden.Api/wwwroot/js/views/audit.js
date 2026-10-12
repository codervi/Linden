import { get } from "../api.js";
import { h, emptyState, pager, fmtStamp } from "../dom.js";

const EVENTS = [
  "LoginSucceeded", "LoginFailed", "ViewClient", "CreateClient", "UpdateClient", "ArchiveClient",
  "RestoreClient", "EraseClient", "ViewNotes", "CreateNote", "UpdateNote", "DeleteNote",
  "CreateAppointment", "UpdateAppointment",
];

export async function render(view) {
  let eventName = "";
  let page = 1;
  const results = h("div");

  async function load() {
    const params = new URLSearchParams({ page });
    if (eventName) params.set("event", eventName);
    const res = await get("/audit?" + params);
    page = res.page;

    results.replaceChildren(
      res.items.length
        ? h("div", { class: "table-wrap" },
            h("table", {},
              h("thead", {}, h("tr", {}, ["When", "User", "Event", "Record", "IP address", "Details"].map((t) => h("th", {}, t)))),
              h("tbody", {}, res.items.map((a) =>
                h("tr", {},
                  h("td", {}, fmtStamp(a.timestampUtc)),
                  h("td", {}, a.userName),
                  h("td", {}, a.action),
                  h("td", {}, a.entityId ? `${a.entityType} #${a.entityId}` : a.entityType),
                  h("td", {}, a.ipAddress),
                  h("td", {}, a.details))))))
        : emptyState("No entries."),
      pager(res.page, res.totalPages, (p) => { page = p; load(); }));
  }

  const filter = h("select", { "aria-label": "Filter by event" },
    h("option", { value: "" }, "All events"),
    EVENTS.map((e) => h("option", { value: e }, e)));
  filter.addEventListener("change", () => { eventName = filter.value; page = 1; load(); });

  view.replaceChildren(
    h("h1", {}, "Audit log"),
    h("p", { class: "muted" }, "Who opened or changed what. The log stores record ids only, never names or note text, and entries can't be edited."),
    h("div", { class: "toolbar" }, filter),
    results);

  await load();
}
