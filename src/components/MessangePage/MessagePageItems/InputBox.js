// File: src/components/MessangePage/MessagePageItems/InputBox.js
import React, { useRef, useEffect } from "react";
import "./InputBox.css";

export default function InputBox({
  value,
  onChange,
  onSend,
  placeholder = "Type your message..."
}) {
  const ref = useRef(null);
  const max = 144; // px (≈ 6 lines)

  /* auto‑resize */
  useEffect(() => {
    if (!ref.current) return;
    ref.current.style.height = "auto";
    ref.current.style.height =
      Math.min(ref.current.scrollHeight, max) + "px";
  }, [value]);

  const send = () => value.trim() && onSend();

  const key = (e) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      send();
    }
  };

  return (
    <div className="input-box">
      <div className="input-wrapper">
        <textarea
          ref={ref}
          className="message-input"
          placeholder={placeholder}
          value={value}
          onChange={onChange}
          onKeyDown={key}
          maxLength={4000}
        />
        <button onClick={send} disabled={!value.trim()}>
          Send
        </button>
      </div>
    </div>
  );
}
