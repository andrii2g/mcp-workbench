export function confirmAction(message) {
  const dialog = document.querySelector("#confirm-dialog"); dialog.querySelector("#confirm-message").textContent = message;
  return new Promise(resolve => { const done = () => { dialog.removeEventListener("close", done); resolve(dialog.returnValue === "confirm"); }; dialog.addEventListener("close", done); dialog.showModal(); });
}
