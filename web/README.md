# React + TypeScript + Vite

This template provides a minimal setup to get React working in Vite with HMR and some ESLint rules.

Currently, two official plugins are available:

- [@vitejs/plugin-react](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react) uses [Oxc](https://oxc.rs)
- [@vitejs/plugin-react-swc](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react-swc) uses [SWC](https://swc.rs/)

## React Compiler

The React Compiler is not enabled on this template because of its impact on dev & build performances. To add it, see [this documentation](https://react.dev/learn/react-compiler/installation).

## Expanding the ESLint configuration

If you are developing a production application, we recommend updating the configuration to enable type-aware lint rules:

```js
export default defineConfig([
  # Frontend da POC de scanner

  Este diretório contém a interface React/TypeScript da POC. A documentação completa, incluindo arquitetura, execução do agente, roteiro de teste físico e análise da solução, está em [README principal](../README.md).

  ## Execução local

  A partir da raiz do projeto:

  ```powershell
  npm --prefix web install
  npm --prefix web run dev
  ```

  Abra o endereço exibido pelo Vite, normalmente `http://localhost:5173`.

  O frontend depende do agente .NET em `http://127.0.0.1:17841`. Consulte o README principal para iniciar o agente e realizar o pareamento.

  ## Verificação

  ```powershell
  npm --prefix web run build
  npm --prefix web run lint
  ```
])
