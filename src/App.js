// File: src/App.js
import React from "react";
import {
  BrowserRouter as Router,
  Routes,
  Route,
  Navigate,
  Outlet
} from "react-router-dom";

import LoginPage      from "./components/LoginPage/LoginPage";
import CreatePinPage  from "./components/CreatePinPage/CreatePinPage";
import EnterPinPage   from "./components/EnterPinPage/EnterPinPage";
import MessangerPage  from "./components/MessangePage/MessangerPage";

/* ── simple auth guard ────────────────────────────────────────────────────── */
function RequireAuth() {
  const token = localStorage.getItem("token");
  return token ? <Outlet /> : <Navigate to="/" replace />;
}

export default function App() {
  return (
    <Router>
      <Routes>
        {/* public */}
        <Route path="/"            element={<LoginPage />} />
        <Route path="/create-pin"  element={<CreatePinPage />} />
        <Route path="/enter-pin"   element={<EnterPinPage />} />

        {/* protected */}
        <Route element={<RequireAuth />}>
          <Route path="/messanger" element={<MessangerPage />} />
        </Route>

        {/* fallback */}
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </Router>
  );
}
