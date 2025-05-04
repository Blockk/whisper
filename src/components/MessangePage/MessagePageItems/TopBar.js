// File: src/components/MessangePage/MessagePageItems/TopBar.js
import React, { useState, useRef, useEffect } from "react";
import "./TopBar.css";

export default function TopBar({ conversationName, username, onLogout }) {
  const [open, setOpen] = useState(false);
  const menuRef         = useRef(null);

  /* close menu when clicking outside */
  useEffect(() => {
    const close = (e) => {
      if (open && menuRef.current && !menuRef.current.contains(e.target)) {
        setOpen(false);
      }
    };
    window.addEventListener("click", close);
    return () => window.removeEventListener("click", close);
  }, [open]);

  return (
    <div className="topbar">
      <div className="conversation-name" title={conversationName}>
        {conversationName}
      </div>

      <div className="user-profile" ref={menuRef}>
        <div className="user-circle" onClick={() => setOpen((v) => !v)}>
          {username?.[0]?.toUpperCase() || "?"}
        </div>

        {open && (
          <div className="user-menu">
            <div className="menu-item disabled">Settings</div>
            <div className="menu-item disabled">Profile</div>
            <div className="menu-item" onClick={onLogout}>
              Logout
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
