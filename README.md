# POC de scanner para web

Esta POC estuda como substituir um serviço legado em Delphi 6 que acessa scanners por TWAIN. O objetivo não é executar Delphi no navegador, mas permitir que uma aplicação web solicite uma digitalização e receba as páginas com uma solução moderna e sustentável.

## Conclusão em uma frase

Uma aplicação exclusivamente web não acessa scanners TWAIN/WIA diretamente; a abordagem recomendada é uma interface web acompanhada de um agente local moderno em .NET. O NAPS2 é o principal candidato para a comunicação com o scanner, mas sua compatibilidade precisa ser validada com hardware real antes da aprovação definitiva.

## Contexto

O serviço atual roda em Delphi 6 e usa uma biblioteca TWAIN. Há dois relatos que orientam esta POC:

1. Nas versões TWAIN 2.2/2.3, a interface do scanner deixou de abrir como abria na versão 2.0.2. É necessário recuperar o acesso do usuário aos recursos oferecidos pela janela do fabricante.
2. Existe um erro no fluxo `Cadastro Completo de Pessoa > Documentação > botão direito > Scanner` no Tasy Native 5.12.

O segundo relato não contém mensagem de erro, modelo do equipamento, versão do driver ou conteúdo do vídeo citado. Esses dados devem ser coletados durante o teste físico.

Também há uma ambiguidade que o teste deve esclarecer: a interface antiga abria imediatamente ao selecionar o equipamento ou depois de iniciar a captura? Nesta POC, o scanner é selecionado na página e a interface do fabricante deve abrir ao clicar em **Digitalizar**.

## Por que não pode ser somente web?

Por segurança, Chrome, Edge e Firefox não oferecem uma API genérica para um site controlar drivers TWAIN ou WIA instalados no computador. O navegador precisa chamar um programa local, executado na máquina do usuário.

```text
Aplicação React no navegador
            |
            | HTTP local autenticado
            v
Agente ASP.NET Core em .NET
            |
            v
NAPS2
            |
            v
TWAIN ou WIA
            |
            v
Driver do fabricante
            |
            v
Scanner físico
```

- **NAPS2** é uma biblioteca .NET que simplifica a descoberta e o controle dos scanners.
- **TWAIN e WIA** são padrões usados por programas para conversar com drivers de scanner.
- **Driver** é o software da Epson, Canon, Brother, HP ou outro fabricante que conhece o equipamento.
- **Agente local** é o programa desta POC que faz a ponte entre o navegador e o NAPS2.

O agente deve rodar como aplicação interativa na sessão do usuário, e não como Windows Service, porque a janela nativa do driver precisa aparecer na área de trabalho do usuário.

## Estrutura do projeto

```text
poc-scanner/
|-- agent/
|   |-- src/ScannerAgent/          API local e integração NAPS2
|   `-- tests/ScannerAgent.Tests/  testes sem hardware
|-- web/                           interface React/TypeScript
`-- poc-scanner.slnx               solução .NET
```

### Interface web

A aplicação React permite:

- parear o navegador com o agente;
- escolher TWAIN ou WIA;
- selecionar scanner, origem do papel, resolução e cor;
- alternar entre interface nativa e modo silencioso;
- iniciar, acompanhar e cancelar uma captura;
- visualizar páginas e miniaturas;
- baixar PDF ou ZIP com as imagens.

### Agente .NET

O agente:

- escuta apenas em `http://127.0.0.1:17841`, sem exposição à rede local;
- aceita as origens locais do frontend em desenvolvimento;
- gera um código de pareamento de seis dígitos no console;
- protege as operações com um token efêmero;
- usa NAPS2 1.3.0 para TWAIN e WIA;
- configura um worker Win32 para drivers TWAIN de 32 bits;
- solicita a interface do fabricante com `UseNativeUI`;
- serializa as capturas para evitar duas operações simultâneas;
- mantém as páginas e exportações numa pasta temporária por sessão;
- exporta cada página em PNG, o documento em PDF e as imagens em ZIP.

## Pré-requisitos para o teste físico

- Windows 10 ou 11.
- Scanner conectado, ligado e reconhecido pelo Windows.
- Driver oficial do fabricante instalado, incluindo TWAIN e/ou WIA.
- .NET SDK 10.
- Node.js com npm.
- Chrome, Edge e Firefox para a matriz completa.

Antes de testar a POC, confirme que o equipamento digitaliza pelo aplicativo **Scanner do Windows** ou pelo software oficial do fabricante. Se não funcionar fora da POC, corrija a instalação do equipamento primeiro.

Para conferir as ferramentas:

```powershell
dotnet --version
node --version
npm --version
```

Se `dotnet` não estiver no `PATH`, esta instalação local também pode ser usada:

```powershell
& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" --version
```

## Como executar

Abra dois terminais PowerShell no diretório raiz do projeto.

### 1. Iniciar o agente

No primeiro terminal:

```powershell
dotnet restore poc-scanner.slnx
dotnet run --project agent/src/ScannerAgent/ScannerAgent.csproj
```

Alternativa quando `dotnet` não estiver no `PATH`:

```powershell
& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" run --project agent/src/ScannerAgent/ScannerAgent.csproj
```

O terminal deve mostrar algo semelhante a:

```text
Scanner Agent pairing code: 123456
Now listening on: http://127.0.0.1:17841
Application started.
```

Guarde o código de seis dígitos e mantenha o terminal aberto. Um novo código e um novo token são gerados quando o agente reinicia.

### 2. Iniciar o frontend

No segundo terminal:

```powershell
npm --prefix web install
npm --prefix web run dev
```

Abra o endereço informado pelo Vite, normalmente:

http://localhost:5173

### 3. Parear e localizar o scanner

1. Informe na página o código exibido pelo agente.
2. Clique em **Parear**.
3. Confirme que a página mostra **Agente conectado**.
4. Selecione `TWAIN` e aguarde a lista de equipamentos.
5. Repita com `WIA` quando o driver oferecer esse padrão.

O seletor fica desabilitado quando nenhum dispositivo foi encontrado. Isso normalmente significa scanner desligado, cabo desconectado ou driver TWAIN/WIA ausente. Não é necessário que o mesmo equipamento apareça nos dois padrões.

## Roteiro de validação com scanner físico

Registre fabricante, modelo, versão e arquitetura do driver antes de começar. Faça capturas apenas com documentos de teste sem dados pessoais ou clínicos.

### Cenário obrigatório: interface do fabricante

1. Selecione `TWAIN`.
2. Escolha o equipamento.
3. Mantenha **Interface do fabricante** habilitada.
4. Clique em **Digitalizar**.
5. Confirme se a janela do fabricante abre e fica visível para o usuário.
6. Altere uma configuração nessa janela, quando possível.
7. Inicie a captura pela janela do fabricante.
8. Confirme que a janela fecha corretamente.
9. Confirme que as páginas aparecem no navegador, na ordem correta.
10. Baixe e abra o PDF e o ZIP de imagens.

Esse é o cenário principal para investigar a regressão observada entre TWAIN 2.0.2 e 2.2/2.3.

### Cenários complementares

- Cancelar pela janela do fabricante sem travar o agente.
- Desabilitar **Interface do fabricante** e testar o modo silencioso.
- Testar 150, 300 e 600 DPI, se suportados.
- Testar colorido, tons de cinza e preto e branco.
- Testar mesa, alimentador e duplex conforme os recursos do equipamento.
- Capturar várias páginas pelo alimentador.
- Desconectar ou desligar o scanner e observar a mensagem de erro.
- Iniciar outra captura depois de cancelamento ou falha.
- Reiniciar o agente, parear novamente e repetir a captura.
- Repetir o cenário principal no Chrome, Edge e Firefox.
- Quando possível, comparar lado a lado com o comportamento da versão TWAIN 2.0.2.

Nem todo scanner suporta alimentador, duplex, todas as resoluções ou os dois padrões. Marque `N/A` quando o recurso não existir no equipamento; não trate isso automaticamente como defeito da POC.

## Registro dos resultados

Copie e preencha uma linha por cenário. Anexe captura de tela, vídeo e logs quando houver falha.

### Ambiente

| Item | Valor |
|---|---|
| Data do teste | |
| Nome do avaliador | |
| Windows e versão | |
| Fabricante/modelo do scanner | |
| Versão do driver | |
| Driver 32 ou 64 bits | |
| Conexão USB ou rede | |
| Funciona no software do fabricante? | |

### Matriz

| Navegador | Driver | Modo | Origem | Cenário | Resultado | Evidência/erro |
|---|---|---|---|---|---|---|
| Chrome | TWAIN | Interface nativa | Mesa | Janela abre e captura | Pendente | |
| Edge | TWAIN | Interface nativa | Mesa | Janela abre e captura | Pendente | |
| Firefox | TWAIN | Interface nativa | Mesa | Janela abre e captura | Pendente | |
| Chrome | TWAIN | Silencioso | Mesa | 300 DPI colorido | Pendente | |
| Chrome | WIA | Interface nativa | Mesa | Janela abre e captura | Pendente | |
| Chrome | TWAIN | Interface nativa | ADF | Múltiplas páginas | Pendente/N/A | |
| Chrome | TWAIN | Interface nativa | Duplex | Frente e verso | Pendente/N/A | |
| Chrome | TWAIN | Interface nativa | Qualquer | Cancelamento e nova captura | Pendente | |

### Critério mínimo para considerar o NAPS2 aprovado nesta POC

- O scanner real é descoberto por TWAIN.
- A interface do fabricante abre de forma utilizável ao clicar em **Digitalizar**.
- Captura e cancelamento não deixam o agente travado.
- As páginas chegam na ordem correta.
- Preview, PDF e ZIP podem ser abertos.
- Uma nova captura funciona após conclusão, cancelamento e erro recuperável.
- O resultado é registrado com modelo e versão do driver.

WIA é uma comparação ou alternativa quando disponível; TWAIN e a interface nativa são os critérios centrais dos bugs apresentados.

## Solução de problemas

### O agente aparece offline

- Confirme que o terminal do agente permanece aberto.
- Verifique se aparece `Now listening on: http://127.0.0.1:17841`.
- Abra `http://127.0.0.1:17841/api/health` no navegador.
- Confirme que outra aplicação não está usando a porta `17841`.

### O código de pareamento não aparece

Pare o agente com `Ctrl+C`, atualize o projeto e execute novamente. O código deve ser impresso durante a inicialização.

### O código é recusado ou a página pede pareamento novamente

- Use o código da execução atual do agente.
- Digite exatamente os seis dígitos.
- Ao reiniciar o agente, pareie novamente; o token anterior deixa de ser válido.

### O seletor de scanner está desabilitado

Isso significa que a enumeração retornou zero dispositivos:

1. ligue e reconecte o scanner;
2. confirme que o Windows reconhece o equipamento;
3. instale o driver oficial completo do fabricante;
4. teste o software do fabricante;
5. reinicie o agente;
6. pareie novamente;
7. alterne entre TWAIN e WIA.

### A janela nativa não abre

- Confirme que **Interface do fabricante** está habilitada.
- Procure a janela atrás do navegador ou na barra de tarefas.
- Execute o agente na sessão do usuário, nunca como Windows Service.
- Registre modelo, versão do driver e se está usando TWAIN ou WIA.
- Verifique se a janela abre no software oficial do fabricante.

### O scanner aparece, mas a captura falha

- Teste inicialmente mesa, 300 DPI e colorido.
- Tente TWAIN e WIA separadamente.
- Não selecione alimentador ou duplex se o equipamento não oferecer o recurso.
- Copie a exceção exibida no agente e a mensagem mostrada na página.

## O que já foi validado

Sem scanner físico, foram validados:

- compilação do agente .NET com NAPS2;
- build e lint do frontend;
- comunicação HTTP entre navegador e agente;
- restrição do host a `127.0.0.1`;
- pareamento e rejeição de chamadas sem token;
- ciclo de sessão com backend falso;
- publicação de página e cancelamento em testes automatizados;
- estrutura de preview, PDF e ZIP no fluxo da aplicação.

Comandos de verificação:

Pare o agente com `Ctrl+C` antes de executar os testes. No Windows, um agente em execução bloqueia a substituição do executável durante a compilação.

```powershell
dotnet test poc-scanner.slnx
npm --prefix web run build
npm --prefix web run lint
```

Alternativa para o teste quando `dotnet` não estiver no `PATH`:

```powershell
& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" test poc-scanner.slnx
```

## O que ainda não foi validado

Sem hardware real, esta POC ainda não comprova:

- descoberta dos scanners usados pelos clientes;
- abertura e posicionamento da interface nativa de cada fabricante;
- equivalência com o comportamento da versão TWAIN 2.0.2;
- compatibilidade real com drivers TWAIN de 32 e 64 bits;
- mesa, alimentador, duplex e múltiplas páginas em equipamentos reais;
- recuperação após atolamento, falta de papel ou falha específica do driver;
- confiabilidade em uso prolongado.

Portanto, o estado correto é **arquitetura implementada e validada sem hardware; compatibilidade de scanner pendente**.

## Comparação com o serviço atual

| Aspecto | Delphi 6 + TWAIN atual | Web + agente .NET + NAPS2 |
|---|---|---|
| Manutenção | Tecnologia antiga, conhecimento mais escasso | C#/.NET e React atuais, ferramentas e profissionais mais disponíveis |
| Experiência web | Não é uma solução web nativa | Interface web integrada ao restante do produto |
| Acesso ao scanner | Direto no processo legado | Feito pelo agente local porque o navegador não acessa TWAIN/WIA diretamente |
| Drivers | Comportamento ligado à biblioteca TWAIN atual | TWAIN e WIA abstraídos pelo NAPS2; compatibilidade ainda precisa de teste físico |
| Interface do fabricante | Existia na versão antiga e sofreu regressão | Solicitada explicitamente por `UseNativeUI`; resultado real ainda precisa de hardware |
| Evolução | Mudanças arriscadas em uma base obsoleta | Contrato web separado da biblioteca de scanner, permitindo trocar o backend |
| Segurança local | Depende da implementação atual | Loopback, CORS e token efêmero já demonstrados na POC |
| Implantação | Serviço/componente legado já distribuído | Exige instalar, iniciar, atualizar e futuramente assinar o agente local |
| Suporte | Conhecimento concentrado no legado | Dependência do NAPS2 e dos drivers dos fabricantes |
| Custo de licença | Depende da biblioteca existente | NAPS2 é open source; obrigações de licença devem ser avaliadas juridicamente |

## Vantagens da abordagem proposta

- Retira Delphi 6 do caminho de evolução do produto.
- Mantém a experiência principal na aplicação web.
- Usa tecnologias atuais e testáveis.
- Isola o acesso ao hardware atrás de um contrato independente do NAPS2.
- Permite trocar o backend no futuro sem reescrever toda a interface.
- Oferece TWAIN e WIA no mesmo fluxo.
- Suporta interface nativa e modo silencioso.
- Permite preview, cancelamento e exportação de maneira uniforme.
- Não expõe o agente à rede local na configuração atual.

## Desvantagens e custos

- Continua sendo necessário instalar um componente local; browser puro não resolve o problema.
- O agente precisa de estratégia de instalação, atualização e suporte.
- Compatibilidade depende dos drivers e modelos de scanner.
- A equipe passa a depender da API e da evolução do NAPS2.
- A POC permite apenas uma captura por vez.
- Reiniciar o agente invalida o pareamento atual.
- Arquivos temporários ainda não possuem expiração ou limpeza automática.
- A comunicação local atual usa HTTP, não HTTPS.
- O código ainda não possui instalador, assinatura digital ou atualização automática.
- Uma solução própria exige matriz de hardware e responsabilidade operacional da equipe.

## Por que esta é a melhor direção neste cenário?

Não existe migração honesta para “somente web” mantendo scanners TWAIN/WIA genéricos. Algum componente local continuará necessário, seja desenvolvido pela equipe ou fornecido comercialmente.

A arquitetura web + agente .NET é a melhor direção porque:

1. elimina a dependência de evolução em Delphi 6;
2. respeita a limitação técnica dos navegadores;
3. separa a aplicação web da tecnologia escolhida para o scanner;
4. permite testar e substituir o backend sem alterar a experiência principal;
5. cria uma base moderna para segurança, observabilidade e distribuição futura.

O NAPS2 é o candidato principal por oferecer TWAIN, WIA, interface nativa, PDF e worker para TWAIN 32 bits sem custo comercial direto. Entretanto, a recomendação final deve ser formulada em duas partes:

- **Arquitetura web + agente .NET:** recomendada para continuar.
- **NAPS2 como biblioteca definitiva:** condicionado à aprovação na matriz com scanners reais.

## Fora do escopo desta POC

- Instalador MSI e assinatura de código.
- Inicialização automática com o Windows.
- Atualização automática do agente.
- HTTPS local e certificado corporativo.
- Persistência ou renovação de tokens.
- Limpeza programada das sessões temporárias.
- Upload para sistemas clínicos ou APIs de produção.
- OCR, edição e anotação de documentos.
- Aprovação jurídica da licença e processo de segurança corporativo.

Esses itens são necessários para produção, mas não devem bloquear a validação inicial da compatibilidade física e da interface nativa.