module.exports = {
  content: { relative: true, files: ['../src/ItAssetManagement.Api/wwwroot/**/*.{html,js}'] },
  theme: {
    extend: {
      colors: {
        primary: '#2563EB',
        'primary-light': '#EAF2FF',
        sidebar: '#F8FAFC',
        surface: '#FFFFFF',
        canvas: '#F6F8FB',
        outline: '#E5E7EB',
        ink: '#1F2937',
        muted: '#64748B',
      },
      fontFamily: { sans: ['Inter', 'system-ui', 'sans-serif'] },
      borderRadius: { card: '12px' },
      boxShadow: { card: '0 4px 20px rgba(31, 41, 55, 0.035)' },
    },
  },
  plugins: [],
};
