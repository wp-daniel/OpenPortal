import { Link } from 'react-router-dom'

export function NotFoundPage() {
  return (
    <div className="mx-auto max-w-lg py-16 text-center">
      <h1 className="text-2xl font-semibold tracking-tight">Page not found</h1>
      <p className="text-muted-foreground mt-2 text-sm">
        The address you followed does not match anything on this portal.
      </p>
      <p className="mt-6">
        <Link to="/" className="underline underline-offset-4">
          Back to the home page
        </Link>
      </p>
    </div>
  )
}