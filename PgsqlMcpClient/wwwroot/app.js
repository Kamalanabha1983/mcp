const { useState } = React;

function App() {
  const [messages, setMessages] = useState([
    { role: "assistant", text: "Ask about your PostgreSQL database. I can use the connected MCP tools to look up live data." }
  ]);
  const [draft, setDraft] = useState("");
  const [busy, setBusy] = useState(false);

  async function sendMessage(event) {
    event.preventDefault();
    const message = draft.trim();
    if (!message || busy) return;

    setDraft("");
    setMessages((current) => [...current, { role: "user", text: message }]);
    setBusy(true);

    try {
      const response = await fetch("/api/chat", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ message })
      });
      const body = await response.text();
      let payload = {};
      if (body) {
        try {
          payload = JSON.parse(body);
        } catch {
          throw new Error(`The agent returned an invalid response (HTTP ${response.status}).`);
        }
      }
      if (!response.ok) throw new Error(payload.error || `The agent request failed (HTTP ${response.status}).`);
      if (!payload.message) throw new Error("The agent returned an empty response.");
      setMessages((current) => [...current, { role: "assistant", text: payload.message }]);
    } catch (error) {
      setMessages((current) => [...current, { role: "error", text: error.message }]);
    } finally {
      setBusy(false);
    }
  }

  return React.createElement("div", { className: "shell" },
    React.createElement("header", { className: "topbar" },
      React.createElement("div", { className: "brand" },
        React.createElement("span", { className: "brand-mark" }, "∿"),
        React.createElement("div", null,
          React.createElement("strong", null, "Postgres AI Console"),
          React.createElement("span", null, "MCP agent workspace")
        )
      ),
      React.createElement("div", { className: "status" },
        React.createElement("span", { className: "status-dot" }), "Connected"
      )
    ),
    React.createElement("section", { className: "workspace" },
      React.createElement("div", { className: "intro" },
        React.createElement("span", { className: "eyebrow" }, "LIVE DATABASE ASSISTANT"),
        React.createElement("h1", null, "What should we find?"),
        React.createElement("p", null, "Natural-language access to your local PostgreSQL MCP server, powered by Azure AI.")
      ),
      React.createElement("div", { className: "conversation" },
        messages.map((item, index) => React.createElement("article", { className: `message ${item.role}`, key: index },
          React.createElement("span", { className: "message-label" }, item.role === "user" ? "You" : item.role === "error" ? "Error" : "Agent"),
          React.createElement("p", null, item.text)
        )),
        busy && React.createElement("article", { className: "message assistant thinking" },
          React.createElement("span", { className: "message-label" }, "Agent"),
          React.createElement("p", null, "Working with the MCP tool…")
        )
      ),
      React.createElement("form", { className: "composer", onSubmit: sendMessage },
        React.createElement("textarea", {
          value: draft,
          onChange: (event) => setDraft(event.target.value),
          onKeyDown: (event) => { if (event.key === "Enter" && !event.shiftKey) sendMessage(event); },
          placeholder: "Try: Which database am I connected to?",
          rows: 2,
          disabled: busy
        }),
        React.createElement("button", { type: "submit", disabled: busy || !draft.trim(), "aria-label": "Send message" }, "↑")
      )
    )
  );
}

ReactDOM.createRoot(document.getElementById("root")).render(React.createElement(App));