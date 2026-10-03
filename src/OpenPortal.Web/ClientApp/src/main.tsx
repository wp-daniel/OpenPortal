import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { App } from './App'
import './index.css'

const container = document.getElementById('root')

if (!container) {
  // Fail here rather than at the first render: a null container means the served HTML does not match the
  // bundle, and a blank page would hide the real cause.
  throw new Error('The #root element is missing from index.html.')
}

createRoot(container).render(
  <StrictMode>
    <App />
  </StrictMode>,
)