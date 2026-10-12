import { get, post, put, patch, del } from "../api.js";
import { h, field, badge, emptyState, toast, fmtDate, fmtTime, fmtStamp, fmtDob, ageFrom } from "../dom.js";
import { clientDialog, appointmentDialog } from "../forms.js";

export async function render(view, { me, params }) {
  const id = Number(params[0]);
  const isAdmin = me.role === "Admin";

  // Opening the client is logged on the server.
  const [client, appointments] = await Promise.all([get(`/clients/${id}`), get(`/clients/${id}/appointments`)]);

  const header = h("div");
  const apptBox = h("div");
  const notesBox = h("div");
  let notes = null; // stays null until the psychologist asks to see them

  const fullName = () => `${client.firstName} ${client.lastName}`;

  // ----- header -----
  function renderHeader() {
    const age = ageFrom(client.dateOfBirth);
    const details = [
      client.dateOfBirth ? `Born ${fmtDob(client.dateOfBirth)} (${age})` : null,
      client.email,
      client.phone,
    ].filter(Boolean);

    header.replaceChildren(
      h("a", { href: "#/clients", class: "back" }, "All clients"),
      h("div", { class: "page-head" },
        h("h1", {}, fullName(), " ", client.isArchived ? badge("Archived") : null),
        h("div", { class: "actions" },
          !client.isArchived && h("button", { class: "btn btn-ghost", onclick: () => clientDialog(client, (c) => { Object.assign(client, c); renderHeader(); }) }, "Edit"),
          client.isArchived
            ? h("button", { class: "btn btn-ghost", onclick: restore }, "Restore")
            : h("button", { class: "btn btn-ghost", onclick: archive }, "Archive"),
          client.isArchived && isAdmin && h("button", { class: "btn btn-danger", onclick: erase }, "Erase permanently"))),
      h("p", { class: "muted" }, details.length ? details.join("  |  ") : "No contact details saved."));
  }

  async function archive() {
    try {
      await post(`/clients/${id}/archive`);
      client.isArchived = true;
      toast("Client archived.");
      renderHeader();
      renderAppointments();
    } catch (err) { toast(err.message, "error"); }
  }

  async function restore() {
    try {
      await post(`/clients/${id}/restore`);
      client.isArchived = false;
      toast("Client restored.");
      renderHeader();
      renderAppointments();
    } catch (err) { toast(err.message, "error"); }
  }

  async function erase() {
    const ok = window.confirm(
      `Permanently erase ${fullName()}?\n\nThe client record, all appointments and all session notes will be deleted. This cannot be undone.`);
    if (!ok) return;
    try {
      await del(`/clients/${id}`);
      toast("Client erased.");
      location.hash = "#/clients";
    } catch (err) { toast(err.message, "error"); }
  }

  // ----- appointments -----
  async function setStatus(a, status) {
    try {
      await patch(`/appointments/${a.id}/status`, { status });
      a.status = status;
      renderAppointments();
    } catch (err) { toast(err.message, "error"); }
  }

  function renderAppointments() {
    apptBox.replaceChildren(
      h("div", { class: "page-head" },
        h("h2", {}, "Appointments"),
        h("button", {
          class: "btn btn-small",
          disabled: client.isArchived,
          onclick: () => appointmentDialog({ client, onSaved: reloadAppointments }),
        }, "Book appointment")),
      appointments.length
        ? h("ul", { class: "agenda" }, appointments.map((a) =>
            h("li", {},
              h("span", { class: "agenda-time" }, `${fmtDate(a.startsAt)}, ${fmtTime(a.startsAt)}`),
              h("span", { class: "muted" }, `${a.durationMinutes} min`),
              badge(a.status),
              a.status === "Scheduled"
                ? h("span", { class: "actions" },
                    h("button", { class: "btn btn-small btn-ghost", onclick: () => setStatus(a, "Completed") }, "Completed"),
                    h("button", { class: "btn btn-small btn-ghost", onclick: () => setStatus(a, "NoShow") }, "No-show"),
                    h("button", { class: "btn btn-small btn-danger", onclick: () => setStatus(a, "Cancelled") }, "Cancel"))
                : null)))
        : emptyState("No appointments yet."));
  }

  async function reloadAppointments() {
    appointments.splice(0, appointments.length, ...(await get(`/clients/${id}/appointments`)));
    renderAppointments();
    if (notes) renderNotes();
  }

  // ----- session notes (psychologist only) -----
  function renderNotesLocked() {
    notesBox.replaceChildren(
      h("h2", {}, "Session notes"),
      isAdmin
        ? h("div", { class: "panel" },
            h("p", {}, "Notes are encrypted and are not loaded until you open them. Opening them is recorded in the audit log."),
            h("button", { class: "btn", onclick: openNotes }, "Show session notes"))
        : h("div", { class: "panel" }, h("p", {}, "Session notes are visible to the psychologist only.")));
  }

  async function openNotes() {
    try {
      notes = await get(`/clients/${id}/notes`);
      renderNotes();
    } catch (err) { toast(err.message, "error"); }
  }

  function apptLabel(appointmentId) {
    const a = appointments.find((x) => x.id === appointmentId);
    return a ? `Session on ${fmtDate(a.startsAt)}` : "Not linked to a session";
  }

  function noteForm(note, onDone) {
    const text = h("textarea", { rows: 6, required: true, maxlength: 8000 }, note?.content ?? "");
    const link = h("select", { value: note?.appointmentId ?? "" },
      h("option", { value: "" }, "Not linked to a session"),
      appointments.map((a) => h("option", { value: a.id }, `${fmtDate(a.startsAt)}, ${fmtTime(a.startsAt)}`)));
    const error = h("p", { class: "form-error", role: "alert" });

    const form = h("form", {
      class: "note-form",
      onsubmit: async (e) => {
        e.preventDefault();
        const button = form.querySelector("button[type=submit]");
        button.disabled = true;
        error.textContent = "";
        const body = { appointmentId: link.value ? Number(link.value) : null, content: text.value };
        try {
          const saved = note ? await put(`/notes/${note.id}`, body) : await post(`/clients/${id}/notes`, body);
          onDone(saved);
        } catch (err) {
          error.textContent = err.message;
          button.disabled = false;
        }
      },
    },
      field(note ? "Edit note" : "New note", text),
      field("Linked session", link),
      error,
      h("div", { class: "form-actions" },
        h("button", { class: "btn", type: "submit" }, "Save note"),
        note ? h("button", { class: "btn btn-ghost", type: "button", onclick: () => onDone(null) }, "Cancel") : null));
    return form;
  }

  function noteCard(n) {
    const card = h("article", { class: "note" });

    function showView() {
      card.replaceChildren(
        h("p", { class: "note-meta" },
          fmtStamp(n.createdAtUtc), "  |  ", apptLabel(n.appointmentId),
          n.updatedAtUtc ? `  |  edited ${fmtStamp(n.updatedAtUtc)}` : ""),
        h("p", { class: "note-text" }, n.content),
        h("div", { class: "actions" },
          h("button", { class: "btn btn-small btn-ghost", onclick: showEdit }, "Edit"),
          h("button", { class: "btn btn-small btn-danger", onclick: remove }, "Delete")));
    }

    function showEdit() {
      card.replaceChildren(noteForm(n, (saved) => {
        if (saved) { Object.assign(n, saved); toast("Note saved."); }
        showView();
      }));
    }

    async function remove() {
      if (!window.confirm("Delete this note? This cannot be undone.")) return;
      try {
        await del(`/notes/${n.id}`);
        notes.splice(notes.indexOf(n), 1);
        toast("Note deleted.");
        renderNotes();
      } catch (err) { toast(err.message, "error"); }
    }

    showView();
    return card;
  }

  function renderNotes() {
    notesBox.replaceChildren(
      h("div", { class: "page-head" },
        h("h2", {}, "Session notes"),
        h("button", { class: "btn btn-small btn-ghost", onclick: () => { notes = null; renderNotesLocked(); } }, "Hide notes")),
      client.isArchived ? null : noteForm(null, (saved) => { notes.unshift(saved); toast("Note saved."); renderNotes(); }),
      ...(notes.length ? notes.map(noteCard) : [emptyState("No notes yet.")]));
  }

  renderHeader();
  renderAppointments();
  renderNotesLocked();
  view.replaceChildren(header, apptBox, notesBox);
}
