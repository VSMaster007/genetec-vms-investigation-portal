/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        ink: '#07111f',
        panel: '#101a2b',
        panel2: '#17263c',
        line: '#2a3d59',
        mist: '#93a4bd',
        paper: '#e7eef8',
        accent: '#3c8dff'
      },
      fontFamily: {
        sans: ['Segoe UI', 'Helvetica Neue', 'sans-serif'],
        mono: ['ui-monospace', 'SFMono-Regular', 'Menlo', 'monospace']
      }
    }
  },
  plugins: []
}
