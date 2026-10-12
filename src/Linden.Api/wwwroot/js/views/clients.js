import { get } from "../api.js";
import { h, badge, emptyState, pager, debounce, fmtDob } from "../dom.js";
import { clientDialog } from "../forms.js";

export async function render(view) {
  let search = "";
  let archived = false;
  let page = 1;
  let requestId = 0;

  const results = h("div");

  async function load() {
    const mine = ++requestId;
    const params = new URLSearchParams({ search, archived, page });
    const res = await get("/clients?" + params);
    if (mine !== requestId) return; // a newer search already replaced this one

    page = res.page;
    results.replaceChildren(
      res.items.length ? table(res.items) : emptyState(archived ? "No archived clients." : "No clients found."),
      h("p", { class: "muted" }, `${res.totalCount} ${res.totalCount === 1 ? "client" : "clients"}`),
      pager(res.page, res.totalPages, (p) => { page = p; load(); }));
  }

  const searchBox = h("input", { type: "search", placeholder: "Search by name or email", "aria-label": "Search clients" });
  searchBox.addEventListener("input", debounce(() => { search = searchBox.value.trim(); page = 1; load(); }));

  const archivedBox = h("input", { type: "checkbox" });
  archivedBox.addEventListener("change", () => { archived = archivedBox.checked; page = 1; load(); });

  view.replaceChildren(
    h("div", { class: "page-head" },
      h("h1", {}, "Clients"),
      h("button", { class: "btn", onclick: () => clientDialog(null, (c) => { location.hash = `#/clients/${c.id}`; }) }, "Add client")),
    h("div", { class: "toolbar" },
      searchBox,
      h("label", { class: "check" }, archivedBox, " Show archived")),
    results);

  await load();
}

function table(items) {
  return h("div", { class: "table-wrap" },
    h("table", {},
      h("thead", {}, h("tr", {}, ["Name", "Contact", "Date of birth", ""].map((t) => h("th", {}, t)))),
      h("tbody", {}, items.map((c) =>
        h("tr", {},
          h("td", {}, h("a", { href: `#/clients/${c.id}` }, `${c.lastName}, ${c.firstName}`)),
          h("td", {}, c.email, c.email && c.phone ? h("br") : null, c.phone),
          h("td", {}, fmtDob(c.dateOfBirth)),
          h("td", {}, c.isArchived ? badge("Archived") : null))))));
}
