// Neon Swarm direct messages (Cloud Code). Deploy: Tools > GD5 > Deploy Cloud Code.
// Writes the message into the recipient's PROTECTED Cloud Save key "inbox" (only the server can write,
// only the owner can read) and a copy into the sender's protected "sent", so nobody else can read DMs.
const { DataApi } = require("@unity-services/cloud-save-1.4");

const MAX_TEXT = 200;
const KEEP = 200;

async function append(api, projectId, playerId, key, entry) {
  for (let attempt = 0; attempt < 4; attempt++) {
    const res = await api.getProtectedItems(projectId, playerId, [key]);
    const item = res.data.results.find(r => r.key === key);
    const list = item && Array.isArray(item.value.items) ? item.value.items : [];
    list.push(entry);
    while (list.length > KEEP) list.shift();
    try {
      await api.setProtectedItem(projectId, playerId, { key, value: { items: list }, writeLock: item ? item.writeLock : undefined });
      return;
    } catch (e) {
      if (!e.response || e.response.status !== 409) throw e;   // 409 = someone wrote in between: reload and retry
    }
  }
  throw new Error("busy, try again");
}

module.exports = async ({ params, context }) => {
  const from = context.playerId;
  const to = String(params.to || "");
  const text = String(params.text || "").trim().slice(0, MAX_TEXT);
  if (!to || to === from) throw new Error("bad recipient");
  if (!text) throw new Error("empty message");

  const api = new DataApi(context);
  const at = Date.now();
  await append(api, context.projectId, to, "inbox", { from, text, at });
  await append(api, context.projectId, from, "sent", { to, text, at });
  return { at };
};

module.exports.params = {
  to: { type: "String", required: true },
  text: { type: "String", required: true },
};
