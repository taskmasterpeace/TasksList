const HOST_NAME = "com.taskslist.browser_context";
let timer;

function scheduleSnapshot() {
  clearTimeout(timer);
  timer = setTimeout(sendSnapshot, 180);
}

async function sendSnapshot() {
  try {
    const windows = await chrome.windows.getAll({ populate: true, windowTypes: ["normal"] });
    const safeWindows = windows
      .filter(window => !window.incognito)
      .map(window => ({
        id: String(window.id),
        focused: Boolean(window.focused),
        tabs: (window.tabs || [])
          .filter(tab => !tab.incognito && /^https?:/i.test(tab.url || ""))
          .map(tab => ({
            id: String(tab.id),
            windowId: String(tab.windowId),
            index: tab.index,
            title: tab.title || tab.url,
            url: tab.url,
            active: Boolean(tab.active),
            pinned: Boolean(tab.pinned),
            incognito: false
          }))
      }));

    await chrome.runtime.sendNativeMessage(HOST_NAME, {
      type: "snapshot",
      browser: "chromium",
      capturedAt: new Date().toISOString(),
      windows: safeWindows
    });
  } catch (error) {
    await chrome.storage.local.set({
      lastError: String(error),
      lastErrorAt: new Date().toISOString()
    });
  }
}

chrome.runtime.onInstalled.addListener(scheduleSnapshot);
chrome.runtime.onStartup.addListener(scheduleSnapshot);
chrome.tabs.onCreated.addListener(scheduleSnapshot);
chrome.tabs.onRemoved.addListener(scheduleSnapshot);
chrome.tabs.onMoved.addListener(scheduleSnapshot);
chrome.tabs.onActivated.addListener(scheduleSnapshot);
chrome.tabs.onUpdated.addListener(scheduleSnapshot);
chrome.windows.onCreated.addListener(scheduleSnapshot);
chrome.windows.onRemoved.addListener(scheduleSnapshot);
chrome.windows.onFocusChanged.addListener(scheduleSnapshot);

chrome.runtime.onMessage.addListener((message, sender, respond) => {
  if (message?.type === "refresh") {
    sendSnapshot().then(() => respond({ ok: true }));
    return true;
  }
  if (message?.type === "openUrls" && Array.isArray(message.urls)) {
    Promise.all(message.urls.map(url => chrome.tabs.create({ url, active: false })))
      .then(() => respond({ ok: true }))
      .catch(error => respond({ ok: false, error: String(error) }));
    return true;
  }
  return false;
});

