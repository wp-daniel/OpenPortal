import ReactMarkdown, { type Components } from 'react-markdown'
import { cn } from '@/lib/utils'

/**
 * Markdown written by the portal's editors (the privacy notice, the terms of use). Raw HTML is never rendered and
 * react-markdown drops `javascript:` and other unsafe link targets, so a document cannot run script; links open in
 * a new tab without passing the portal as the referrer.
 */
const components: Components = {
  h1: ({ node: _node, ...props }) => <h2 className="mt-8 mb-3 text-xl font-semibold tracking-tight first:mt-0" {...props} />,
  h2: ({ node: _node, ...props }) => <h3 className="mt-6 mb-2 text-lg font-semibold tracking-tight first:mt-0" {...props} />,
  h3: ({ node: _node, ...props }) => <h4 className="mt-5 mb-2 font-semibold first:mt-0" {...props} />,
  p: ({ node: _node, ...props }) => <p className="my-3 leading-relaxed" {...props} />,
  ul: ({ node: _node, ...props }) => <ul className="my-3 list-disc space-y-1 pl-6" {...props} />,
  ol: ({ node: _node, ...props }) => <ol className="my-3 list-decimal space-y-1 pl-6" {...props} />,
  a: ({ node: _node, ...props }) => (
    <a className="text-primary underline underline-offset-4" target="_blank" rel="noopener noreferrer" {...props} />
  ),
  blockquote: ({ node: _node, ...props }) => <blockquote className="text-muted-foreground my-3 border-l-2 pl-4" {...props} />,
  code: ({ node: _node, ...props }) => <code className="bg-muted rounded px-1 py-0.5 font-mono text-[0.85em]" {...props} />,
  hr: ({ node: _node, ...props }) => <hr className="my-6" {...props} />,
  table: ({ node: _node, ...props }) => (
    <div className="my-4 overflow-x-auto">
      <table className="w-full border-collapse text-left text-sm" {...props} />
    </div>
  ),
  th: ({ node: _node, ...props }) => <th className="border-b px-2 py-1.5 font-medium" {...props} />,
  td: ({ node: _node, ...props }) => <td className="border-b px-2 py-1.5" {...props} />,
}

export function Markdown({ children, className }: { children: string; className?: string }) {
  return (
    <div className={cn('text-sm break-words', className)}>
      <ReactMarkdown components={components} skipHtml>
        {children}
      </ReactMarkdown>
    </div>
  )
}
