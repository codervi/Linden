import { post } from "../api.js";
import { h, field } from "../dom.js";

export function renderLogin(root, onSuccess, message = "") {
  const email = h("input", { type: "email", required: true, autocomplete: "username" });
  const password = h("input", { type: "password", required: true, autocomplete: "current-password" });
  const error = h("p", { class: "form-error", role: "alert" }, message);

  const form = h("form", {
    class: "login-form",
    onsubmit: async (e) => {
      e.preventDefault();
      const button = form.querySelector("button");
      error.textContent = "";
      button.disabled = true;
      try {
        const me = await post("/auth/login", { email: email.value, password: password.value });
        onSuccess(me);
      } catch (err) {
        error.textContent = err.message;
        password.value = "";
        button.disabled = false;
      }
    },
  },
    field("Email", email),
    field("Password", password),
    error,
    h("button", { class: "btn btn-block", type: "submit" }, "Sign in"));

  root.replaceChildren(
    h("main", { id: "view", class: "login" },
      h("h1", {}, "Linden Practice Manager"),
      h("p", { class: "muted" }, "Staff sign in. Client records are confidential and every access is logged."),
      form));
  email.focus();
}
