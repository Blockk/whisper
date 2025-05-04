// File: src/components/CreatePinPage/CreatePinPage.js
import React, { useState, useEffect } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import "./CreatePinPage.css";
import { post, setToken } from "../../api";

export default function CreatePinPage() {
  /* ── creds handed over from previous step or sessionStorage ───────────── */
  const { state } = useLocation();
  const nav       = useNavigate();

  const username  = state?.username || sessionStorage.getItem("auth_username");
  const password  = state?.password || sessionStorage.getItem("auth_password");

  useEffect(() => {
    if (!username || !password) nav("/", { replace: true });
  }, [username, password, nav]);

  /* ── local ui state ───────────────────────────────────────────────────── */
  const [loginStage,      setLoginStage]      = useState("first");
  const [loginPinFirst,   setLoginPinFirst]   = useState("");
  const [loginPinConfirm, setLoginPinConfirm] = useState("");
  const [loginConflict,   setLoginConflict]   = useState("");
  const [loginShake,      setLoginShake]      = useState(false);

  const [emStage,         setEmStage]         = useState("first");
  const [emPinFirst,      setEmPinFirst]      = useState("");
  const [emPinConfirm,    setEmPinConfirm]    = useState("");
  const [emConflict,      setEmConflict]      = useState("");
  const [emShake,         setEmShake]         = useState(false);

  const [lastSide,        setLastSide]        = useState("");
  const digits = ["1","2","3","4","5","6","7","8","9"];

  /* ── helpers ──────────────────────────────────────────────────────────── */
  const shakeReset = (side) => {
    side === "login" ? setLoginShake(true) : setEmShake(true);
    setTimeout(() => {
      if (side === "login") {
        setLoginShake(false);
        setLoginPinFirst("");
        setLoginPinConfirm("");
        setLoginStage("first");
        setLoginConflict("");
      } else {
        setEmShake(false);
        setEmPinFirst("");
        setEmPinConfirm("");
        setEmStage("first");
        setEmConflict("");
      }
      side === "login" ? setLoginConflict("") : setEmConflict("");
    }, 900);
  };

  /* ── login pad ─────────────────────────────────────────────────────────── */
  const loginDigit = (d) => {
    if (loginStage === "first" && loginPinFirst.length < 6) {
      const p = loginPinFirst + d;
      setLoginPinFirst(p);
      if (p.length === 6) setTimeout(() => setLoginStage("confirm"), 250);
    } else if (loginStage === "confirm" && loginPinConfirm.length < 6) {
      const p = loginPinConfirm + d;
      setLoginPinConfirm(p);
      if (p.length === 6) {
        if (p === loginPinFirst) setLoginStage("confirmed");
        else { setLoginConflict("Pins do not match"); shakeReset("login"); }
      }
    }
    setLastSide("login");
  };
  const loginDel = () =>
    loginStage === "first"
      ? setLoginPinFirst((s) => s.slice(0, -1))
      : setLoginPinConfirm((s) => s.slice(0, -1));

  /* ── emergency pad ────────────────────────────────────────────────────── */
  const emDigit = (d) => {
    if (emStage === "first" && emPinFirst.length < 6) {
      const p = emPinFirst + d;
      setEmPinFirst(p);
      if (p.length === 6) setTimeout(() => setEmStage("confirm"), 250);
    } else if (emStage === "confirm" && emPinConfirm.length < 6) {
      const p = emPinConfirm + d;
      setEmPinConfirm(p);
      if (p.length === 6) {
        if (p === emPinFirst) setEmStage("confirmed");
        else { setEmConflict("Pins do not match"); shakeReset("em"); }
      }
    }
    setLastSide("emergency");
  };
  const emDel = () =>
    emStage === "first"
      ? setEmPinFirst((s) => s.slice(0, -1))
      : setEmPinConfirm((s) => s.slice(0, -1));

  /* ── forbid identical login/delete pins (after first entry) ───────────── */
  useEffect(() => {
    if (loginPinFirst.length === 6 && emPinFirst.length === 6 && loginPinFirst === emPinFirst) {
      if (lastSide === "login") {
        setLoginConflict("Pins must differ");
        shakeReset("login");
      } else {
        setEmConflict("Pins must differ");
        shakeReset("em");
      }
    }
  }, [loginPinFirst, emPinFirst, lastSide]);

  /* ── when both confirmed → register account ───────────────────────────── */
  const ready = loginStage === "confirmed" && emStage === "confirmed";

  useEffect(() => {
    if (!ready) return;

    (async () => {
      try {
        const { token } = await post("/auth/register", {
          username,
          password,
          loginPin:  loginPinFirst,
          deletePin: emPinFirst
        });
        setToken(token);
        sessionStorage.clear();
        nav("/enter-pin", { state: { username, password } });
      } catch (err) {
        alert(err.message || "Registration failed");
        nav("/", { replace: true });
      }
    })();
  }, [ready, username, password, loginPinFirst, emPinFirst, nav]);

  /* ── ui ────────────────────────────────────────────────────────────────── */
  return (
    <div className="create-pin-page">
      {/* login side */}
      <div className="half-container left-half">
        <div className={`pin-panel ${loginShake ? "shake" : ""}`}>
          {loginConflict && <div className="error-msg">{loginConflict}</div>}
          {loginStage !== "confirmed" ? (
            <>
              <h2>{loginStage === "first" ? "Create Login PIN" : "Re‑enter Login PIN"}</h2>
              <div className="pin-display">
                {Array.from({ length: 6 }).map((_, i) => (
                  <div key={i} className="pin-dot">
                    {loginStage === "first"   ? (loginPinFirst[i]   ? "•" : "")
                    : loginStage === "confirm" ? (loginPinConfirm[i] ? "•" : "")
                    : ""}
                  </div>
                ))}
              </div>
              <div className="keypad">
                <div className="digits-grid">
                  {digits.map((d) => (
                    <button key={d} className="digit-btn" onClick={() => loginDigit(d)}>
                      {d}
                    </button>
                  ))}
                </div>
                <div className="zero-row">
                  <button className="digit-btn" onClick={() => loginDigit("0")}>0</button>
                  <button className="digit-btn back-btn" onClick={loginDel}>←</button>
                </div>
              </div>
              <div className="description-text">You’ll enter this PIN to log in.</div>
            </>
          ) : (
            <div className="large-check">✓</div>
          )}
        </div>
      </div>

      {/* emergency side */}
      <div className="half-container right-half">
        <div className={`pin-panel ${emShake ? "shake" : ""}`}>
          {emConflict && <div className="error-msg">{emConflict}</div>}
          {emStage !== "confirmed" ? (
            <>
              <h2>{emStage === "first" ? "Create Emergency Delete PIN" : "Re‑enter Emergency PIN"}</h2>
              <div className="pin-display">
                {Array.from({ length: 6 }).map((_, i) => (
                  <div key={i} className="pin-dot">
                    {emStage === "first"   ? (emPinFirst[i]   ? "•" : "")
                    : emStage === "confirm" ? (emPinConfirm[i] ? "•" : "")
                    : ""}
                  </div>
                ))}
              </div>
              <div className="keypad">
                <div className="digits-grid">
                  {digits.map((d) => (
                    <button key={d} className="digit-btn" onClick={() => emDigit(d)}>
                      {d}
                    </button>
                  ))}
                </div>
                <div className="zero-row">
                  <button className="digit-btn" onClick={() => emDigit("0")}>0</button>
                  <button className="digit-btn back-btn" onClick={emDel}>←</button>
                </div>
              </div>
              <div className="description-text">
                Entering this PIN at login will wipe the account and chats.
              </div>
            </>
          ) : (
            <div className="large-check">✓</div>
          )}
        </div>
      </div>

      <div className="vertical-line" />
    </div>
  );
}
