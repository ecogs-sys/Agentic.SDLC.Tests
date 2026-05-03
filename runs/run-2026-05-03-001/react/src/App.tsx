import { BrowserRouter, Link, Route, Routes } from 'react-router-dom'
import { ContactPage } from './pages/ContactPage'

function HomePage() {
  return (
    <main>
      <h1>Welcome</h1>
      <p>
        <Link to="/contact">Contact Us</Link>
      </p>
    </main>
  )
}

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/contact" element={<ContactPage />} />
      </Routes>
    </BrowserRouter>
  )
}

export default App
