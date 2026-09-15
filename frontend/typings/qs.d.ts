declare module 'qs' {
  export function parse(query: string, options?: unknown): Record<string, unknown>;
  export function stringify(value: unknown, options?: unknown): string;
}
