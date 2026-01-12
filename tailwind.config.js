/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./Views/**/*.cshtml",
    "./Areas/**/*.cshtml",
    "./Pages/**/*.cshtml",
    "./wwwroot/js/**/*.js",
    "./Frontend/**/*.html",
    "./Frontend/**/*.js"
  ],
  safelist: [
    // booked
    "bg-slate-800",
    "border-slate-800",
    "bg-slate-900",
    "border-slate-900",
    "text-white",
    "cursor-not-allowed",
    "line-through",
    "pointer-events-none",
    "opacity-90",
    "opacity-100",
    "shadow-sm",

    // active (đang chọn)
    "bg-indigo-700",
    "border-indigo-700",
    "shadow-md",
    "ring-2",
    "ring-indigo-300",

    // available hover
    "hover:bg-slate-50"
  ],
  theme: { extend: {} },
  plugins: [
    require("daisyui"),
    require("flowbite/plugin")
  ],
  daisyui: {
    themes: ["light"]
  }
};
