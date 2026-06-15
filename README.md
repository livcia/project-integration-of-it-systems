# Project Integration of IT Systems

[![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Aplikacja webowa typu Jira/Kanban do zarządzania zadaniami zespołu, napisana w .NET 10 z użyciem Blazor Server.

Live: https://jira-integration-system.onrender.com

## Opis

System umożliwia tworzenie tablic projektowych, zarządzanie zadaniami, przypisywanie użytkowników, komentowanie zadań oraz pracę na widoku Kanban z drag-and-drop. Obsługuje logowanie społecznościowe przez Google i GitHub, powiadomienia e-mail przez SendGrid oraz dodatkowe integracje, takie jak Open-Meteo i Dicebear.

## Stos technologiczny

- Platforma: .NET 10
- UI: Blazor Server, Razor Components
- Baza danych: PostgreSQL

## Główne funkcjonalności

- Zarządzanie użytkownikami i logowanie OAuth przez Google i GitHub
- Tworzenie, edycja i usuwanie tablic oraz zadań
- Role w tablicy: admin, member, viewer
- Kanban board z kolumnami i drag-and-drop
- Komentarze do zadań z możliwością edycji
- Powiadomienia e-mail przy przypisaniu zadania przez SendGrid
- Pobieranie pogody przez Open-Meteo
- Automatyczne generowanie avatarów przez Dicebear

## Architektura i model danych

Aplikacja działa jako Blazor Server, więc frontend i backend są realizowane w jednym projekcie, a nie jako oddzielne SPA z REST API. Relacyjny model danych obejmuje encje: Uzytkownicy, Tablice, TablicaUzytkownik, Zadania i Komentarze.

## Uruchomienie

1. Sklonuj repozytorium:
   ```bash
   git clone https://github.com/livcia/project-integration-of-it-systems.git
   cd project-integration-of-it-systems
   ```

2. Skopiuj plik konfiguracyjny:
   ```bash
   cp .env.example .env
   ```
   Następnie uzupełnij plik .env własnymi wartościami.

3. Uruchom aplikację w Dockerze:
   ```bash
   docker-compose up -d --build
   ```

4. Otwórz aplikację w przeglądarce:
   http://localhost:6767/

---
### Autorzy
- Oliwia Ankiewicz
- Oliwia Spaleniak
- Malwina Zabielska
