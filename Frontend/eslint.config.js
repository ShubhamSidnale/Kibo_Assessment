import typescriptParser from '@typescript-eslint/parser';

export default [
    { ignores: ['node_modules/**', 'dist/**', '*.tsbuildinfo'] },
    {
        files: ['**/*.{js,ts,tsx}'],
        languageOptions: {
            parser: typescriptParser,
            ecmaVersion: 'latest',
            sourceType: 'module',
            parserOptions: { ecmaFeatures: { jsx: true } },
        },
        rules: {
            curly: ['error', 'all'],
            'max-len': ['error', { code: 120, tabWidth: 4 }],
        },
    },
];
