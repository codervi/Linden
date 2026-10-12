// Dialogs shared by several screens.
import { get, post, put } from "./api.js";
import { h, field, modal, toast, debounce, isoDate, fmtTime } from "./dom.js";

function formActions(close, label = "Save") {
  return h("div", { class: "form-actions" },
    h("button", { class: "btn", type: "submit" }, label),
    h("button", { class: "btn btn-ghost", type: "button", onclick: close }, "Cancel"));
}

// Runs an async save while the submit button is disabled, so a double click can't send it twice.
async function submitting(form, error, work) {
  const button = form.querySelector("button[type=submit]");
  error.textContent = "";
  button.disabled = true;
  try {
    await work();
  } catch (err) {
    error.textContent = err.message;
  } finally {
    button.disabled = false;
  }
}

export function clientDialog(existing, onSaved) {
  modal(existing ? "Edit client" : "New client", (close) => {
    const first = h("input", { required: true, maxlength: 80, autocomplete: "off", value: existing?.firstName ?? "" });
    const last = h("input", { required: true, maxlength: 80, autocomplete: "off", value: existing?.lastName ?? "" });
    const email = h("input", { type: "email", maxlength: 120, autocomplete: "off", value: existing?.email ?? "" });
    const phone = h("input", { type: "tel", maxlength: 30, autocomplete: "off", value: existing?.phone ?? "" });
    const dob = h("input", { type: "date", max: isoDate(new Date()), value: existing?.dateOfBirth ?? "" });
    const error = h("p", { class: "form-error", role: "alert" });

    const form = h("form", {
      onsubmit: (e) => {
        e.preventDefault();
        submitting(form, error, async () => {
          const body = {
            firstName: first.value,
            lastName: last.value,
            email: email.value || null,
            phone: phone.value || null,
            dateOfBirth: dob.value || null,
          };
          const saved = existing ? await put(`/clients/${existing.id}`, body) : await post("/clients", body);
          close();
          toast(existing ? "Client updated." : "Client added.");
          onSaved(saved);
        });
      },
    },
      h("div", { class: "row" }, field("First name", first), field("Last name", last)),
      field("Email (optional)", email),
      h("div", { class: "row" }, field("Phone (optional)", phone), field("Date of birth (optional)", dob)),
      error,
      formActions(close));
    return form;
  });
}

// "client" can be given when booking from a client's page.
// Without it, a small search box lets the user pick one.
export function appointmentDialog({ client = null, date = "", onSaved }) {
  modal("New appointment", (close) => {
    const error = h("p", { class: "form-error", role: "alert" });

    const search = h("input", { type: "search", placeholder: "Type a name to search", autocomplete: "off" });
    const select = h("select", { required: true }, h("option", { value: "" }, "Select a client"));
    const clientField = client
      ? h("p", { class: "fixed-value" }, `${client.firstName} ${client.lastName}`)
      : h("div", {}, search, select);

    async function loadClients() {
      try {
        const res = await get(`/clients?search=${encodeURIComponent(search.value)}`);
        select.replaceChildren(
          h("option", { value: "" }, res.items.length ? "Select a client" : "No clients found"),
          ...res.items.map((c) => h("option", { value: c.id }, `${c.lastName}, ${c.firstName}`)));
      } catch (err) {
        error.textContent = err.message;
      }
    }
    if (!client) {
      search.addEventListener("input", debounce(loadClients));
      loadClients();
    }

    const day = h("input", { type: "date", required: true, min: isoDate(new Date()), value: date });
    const time = h("input", { type: "time", required: true, step: 300, min: "08:00", max: "18:30" });
    const duration = h("select", { value: "50" },
      [30, 45, 50, 60, 90].map((m) => h("option", { value: m }, `${m} minutes`)));

    const form = h("form", {
      onsubmit: (e) => {
        e.preventDefault();
        submitting(form, error, async () => {
          const clientId = client ? client.id : Number(select.value);
          if (!clientId) throw new Error("Select a client.");
          await post("/appointments", {
            clientId,
            startsAt: `${day.value}T${time.value}:00`,
            durationMinutes: Number(duration.value),
          });
          close();
          toast(`Appointment booked for ${fmtTime(`${day.value}T${time.value}:00`)}.`);
          onSaved();
        });
      },
    },
      field("Client", clientField),
      h("div", { class: "row" }, field("Date", day), field("Start time", time, "Open 08:00 to 19:00, Monday to Friday")),
      field("Length", duration),
      error,
      formActions(close, "Book appointment"));
    return form;
  });
}
