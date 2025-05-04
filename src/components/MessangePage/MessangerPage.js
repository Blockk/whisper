// File: src/components/MessangePage/MessangerPage.js
import React, { useState, useEffect, useRef, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import Conversations from "./MessagePageItems/Conversations";
import InputBox      from "./MessagePageItems/InputBox";
import TopBar        from "./MessagePageItems/TopBar";
import { get, clearToken } from "../../api";
import "./MessangerPage.css";

const API = process.env.REACT_APP_API || "http://137.184.12.91:5211";

export default function MessangerPage() {
  /* ── state ───────────────────────────────────────────────────────────── */
  const [convList, setConvList] = useState([]);
  const [selected, setSelected] = useState(null);   // guid
  const [messages, setMessages] = useState([]);
  const [text, setText]         = useState("");
  const [connected, setConnected] = useState(false);
  
  const navigate = useNavigate();
  const bottomRef   = useRef(null);
  const hubRef      = useRef(null);
  const token       = localStorage.getItem("token");
  const myId        = localStorage.getItem("userId");

  /* ── helper: scroll chat to latest ───────────────────────────────────── */
  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages]);

  /* ── load conversation list (initial + every 30 s) ───────────────────── */
  const loadConvs = useCallback(() => {
    get("/conversations")
      .then(setConvList)
      .catch(err => {
        if (err.message === "Unauthorized") {
          // now do your React‑Router logout
          clearToken();
          localStorage.removeItem("userId");
          localStorage.removeItem("username");
          navigate("/", { replace: true });
        } else {
          console.error(err);
        }
      });
  }, [navigate]);
  

  useEffect(() => {
    loadConvs();
    const id = setInterval(loadConvs, 30_000);
    return () => clearInterval(id);
  }, [loadConvs]);

  /* ── establish SignalR connection once ──────────────────────────────── */
  useEffect(() => {
    const hub = new HubConnectionBuilder()
      .withUrl(`${API}/hub/chat`, {
        accessTokenFactory: () => token,
        withCredentials: false
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Information)
      .build();

    hub.start()
      .then(() => { setConnected(true); console.info("🔌 SignalR connected"); })
      .catch(console.error);

    /* new message from server */
    hub.on("Receive", (msg) => {
      setMessages((prev) =>
        msg.conversationId === selected ? [...prev, msg] : prev
      );
      loadConvs(); // refresh sidebar counters / last message
    });

    /* keep selected room joined on reconnect */
    hub.onreconnected(() => {
      if (selected) hub.invoke("JoinConversation", selected).catch(console.error);
    });

    hubRef.current = hub;
    return () => hub.stop();
  }, [token, selected, loadConvs]);

  /* ── select a conversation ──────────────────────────────────────────── */
  const fetchHistory = useCallback((convId) => {
    get(`/messages/${convId}`)
      .then(setMessages)
      .then(() => {
        hubRef.current?.invoke("MarkRead", convId).catch(() => {});
        loadConvs(); // clear unread badge
      })
      .catch(console.error);
  }, [loadConvs]);

  const handleSelect = (convId) => {
    if (selected) hubRef.current?.invoke("LeaveConversation", selected).catch(() => {});
    setSelected(convId);
    setMessages([]);
    hubRef.current?.invoke("JoinConversation", convId).catch(console.error);
    fetchHistory(convId);
  };

  /* ── send message ───────────────────────────────────────────────────── */
  const handleSend = () => {
    if (!text.trim() || !selected) return;
    hubRef.current
      ?.invoke("SendMessage", selected, text.trim())
      .then(() => setText(""))
      .catch(console.error);
  };

  /* ── logout ─────────────────────────────────────────────────────────── */
  const handleLogout = () => {
    clearToken();
    localStorage.removeItem("userId");
    localStorage.removeItem("username");
    window.location.href = "/";
  };

  /* ── render ─────────────────────────────────────────────────────────── */
  return (
    <div className="messenger-page">
      <div className="sidebar">
        <Conversations
          items={convList}
          active={selected}
          onSelect={handleSelect}
        />
      </div>

      <div className="chat-area">
        <TopBar
          conversationName={
            convList.find((c) => c.conversationId === selected)?.title ??
            "Select a chat"
          }
          username={localStorage.getItem("username")}
          onLogout={handleLogout}
        />

        <div className="chat-messages">
          {messages.map((m) => (
            <div
              key={m.id}
              className={`message ${m.senderId === myId ? "from-me" : "from-them"}`}
            >
              {m.body}
            </div>
          ))}
          <div ref={bottomRef} />
        </div>

        <InputBox
          value={text}
          onChange={(e) => setText(e.target.value)}
          onSend={handleSend}
          placeholder={connected ? "Type your message..." : "Connecting..."}
        />
      </div>
    </div>
  );
}
