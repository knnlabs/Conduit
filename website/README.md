# Conduit Documentation

This website is built using [Docusaurus](https://docusaurus.io/), a modern static website generator for Conduit's documentation.

## Installation

```bash
# Using npm
npm install

# Using Yarn
yarn
```

## Local Development

```bash
# Using npm
npm start

# Using Yarn
yarn start
```

This command starts a local development server and opens up a browser window. Most changes are reflected live without having to restart the server.

## Build

```bash
# Using npm
npm run build

# Using Yarn
yarn build
```

This command generates static content into the `build` directory that can be served by any static hosting service.

## Deployment

The documentation site is automatically deployed to GitHub Pages through a GitHub Actions workflow whenever changes are pushed to the `master` branch.

The site should be available at: https://knnlabs.github.io/Conduit/

## Contributing

When contributing to the documentation:

1. Always preview your changes locally before pushing
2. Follow the established structure and formatting
3. Include appropriate front matter in all pages
4. Use Markdown features like admonitions, tabs, and code blocks for better readability