document.querySelector("#refresh").addEventListener("click", async () => {
  const status = document.querySelector("#status");
  const result = await chrome.runtime.sendMessage({ type: "refresh" });
  status.textContent = result?.ok ? "Task'sList was refreshed." : "The local Task'sList bridge is not connected.";
});

