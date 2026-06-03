# project-integration-of-it-systems



tasks na najbliższe 3 dni:  
Malwina -> CI/CD testy automatyczne po pushu  (.yml)  
Olanki - Drugi projekt w tym samym repo do testow  
OliwiaS - Architektura (foldery drzewko komponenty)  


### 🟢 Dzień 1: Fundamenty i Dostęp
* **Olanki:** Postawienie bazy danych (PostgreSQL) w izolowanym środowisku (Docker). Zaprojektowanie głównej struktury danych (jak będą zapisywani użytkownicy, tablice i zadania).
* **OliwiaS:** Wdrożenie logowania do aplikacji za pomocą konta GitHub. Stworzenie głównego szkieletu wizualnego aplikacji (board)
* **Malwina:** Konfiguracja narzędzi do testowania całej aplikacji z perspektywy użytkownika (E2E). Napisanie testów automatycznych dla procesu logowania.

### 🟡 Dzień 2: Użytkownicy i Tablice (Przestrzenie robocze)
* **Olanki:** Stworzenie "zaplecza" (logiki) do tworzenia nowych tablic (projektów) oraz dodawania do nich zalogowanych użytkowników.
* **OliwiaS:** Stworzenie ekranów w aplikacji, na których użytkownik może założyć swoją tablicę projektową, zobaczyć listę swoich tablic i zaprosić do nich współpracowników.
* **Malwina:** napisanie testów sprawdzających, czy tablice tworzą się poprawnie i czy użytkownicy są do nich właściwie przypisywani (testy jednostkowe i integracyjne). Czy widzą te tablice

### 🟠 Dzień 3: Tablica Kanban i Wyszukiwarka
* **Olanki:** Stworzenie zaplecza dla zadań (zapisywanie zmian statusów: To Do -> In Progress -> In Review -> Done). Wdrożenie zaawansowanej wyszukiwarki tekstu w oparciu o bazę danych.
* **OliwiaS:** Budowa interfejsu tablicy Kanban. Stworzenie widoku, w którym użytkownik może dodawać nowe zadania i w wygodny sposób przesuwać je pomiędzy kolumnami statusów.
* **Malwina:** Testowanie przepływu zadań – sprawdzanie automatyczne, czy zadanie poprawnie zmienia statusy i nie znika z tablicy. Testowanie skuteczności wyszukiwarki.

### 🔴 Dzień 4: Szlify, Jakość i Zakończenie
* **Olanki:** Wsparcie zespołu przy łączeniu wszystkich elementów. Rozwiązywanie najtrudniejszych problemów z bazą danych i logiką, optymalizacja aplikacji.
* **OliwiaS:** Podpięcie  wyszukiwarki do interfejsu, ostateczne poprawki wyglądu aplikacji. Upewnienie się, że widoki przechodzą testy.
* **Malwina:** Finałowe testy przeklikujące całą ścieżkę (E2E): od logowania, przez stworzenie tablicy, aż po zamknięcie zadania. Upewnienie się, że spełniamy wymóg 80% pokrycia kodu testami. Z
