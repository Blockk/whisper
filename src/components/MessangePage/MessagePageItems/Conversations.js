// File: src/components/MessangePage/MessagePageItems/Conversations.js
import React from "react";
import "./Conversations.css";
import newIcon from "../../../assets/NewConversation.png";
import { post } from "../../../api";

export default function Conversations({ items, active, onSelect, refresh }) {
  /* new chat prompt ------------------------------------------------------- */
  const createConversation = async () => {
    const uname = prompt("Enter a username to chat with:");
    if (!uname) return;

    try {
      const { conversationId } = await post("/conversations", {
        targetUsername: uname.trim()
      });
      await refresh?.();              // refresh sidebar list
      onSelect(conversationId);       // open the newly created chat
    } catch (err) {
      alert(err.message || "Unable to create conversation.");
    }
  };

  /* helpers --------------------------------------------------------------- */
  const snippet = (txt) =>
    txt ? (txt.length > 32 ? txt.slice(0, 31) + "…" : txt) : "No messages yet";

  return (
    <div className="conversations-sidebar">
      {/* header */}
      <div className="sidebar-header">
        <span className="create-text">New Chat</span>
        <button className="create-conversation-btn" onClick={createConversation}>
          <img src={newIcon} alt="New" className="icon-img" />
        </button>
      </div>

      {/* list */}
      <div className="conversation-list">
        <h3>Chats</h3>

        {items.length === 0 && (
          <p className="empty-note">No conversations yet</p>
        )}

        {items.map((c) => (
          <button
            key={c.conversationId}
            onClick={() => onSelect(c.conversationId)}
            className={`conversation-item ${
              active === c.conversationId ? "active" : ""
            }`}
          >
            <div className="avatar">
              {c.title?.[0]?.toUpperCase() || "?"}
            </div>

            <div className="conversation-info">
              <h4>{c.title}</h4>
              <p>{snippet(c.last)}</p>
            </div>

            {c.unread > 0 && (
              <span className="badge">{c.unread > 9 ? "9+" : c.unread}</span>
            )}
          </button>
        ))}
      </div>
    </div>
  );
}
