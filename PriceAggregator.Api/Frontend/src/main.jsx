import React from 'react';
import ReactDOM from 'react-dom/client';
import App from './App.jsx';
import './style.css';

const rootElement = document.getElementById('root');
if (rootElement && !window.__REACT_APP_MOUNTED__) {
  window.__REACT_APP_MOUNTED__ = true;
  ReactDOM.createRoot(rootElement).render(
    <React.StrictMode>
      <App />
    </React.StrictMode>
  );
}
