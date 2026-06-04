# project-integration-of-it-systems


tasks na najblizsze 3 dni:  
Malwina -> CI/CD testy automatyczne po pushu  (.yml)  
Olanki - Drugi projekt w tym samym repo do testow  
OliwiaS - Architektura (foldery drzewko komponenty)  

## Diagram ERD bazy danych:
  
```mermaid
erDiagram
    UZYTKOWNICY ||--o{ TABLICE : "tworzy (owner)"
    UZYTKOWNICY ||--o{ TABLICE_UZYTKOWNICY : "uczestniczy"
    TABLICE ||--o{ TABLICE_UZYTKOWNICY : "zawiera"
    TABLICE ||--o{ ZADANIA : "posiada"
    UZYTKOWNICY ||--o{ ZADANIA : "tworzy"
    UZYTKOWNICY ||--o{ ZADANIA : "przypiszemy"
    ZADANIA ||--o{ KOMENTARZE : "ma"
    UZYTKOWNICY ||--o{ KOMENTARZE : "pisze"

    UZYTKOWNICY {
        int id_uzytkownika PK
        string email UK
        string password
        string nazwa_uzytkownika
        string avatar_url
        string github_id
        string google_id
        string github_token
        string google_token
        timestamp data_rejestracji
    }

    TABLICE {
        int id_tablicy PK
        string nazwa_tablicy
        string opis_tablicy
        int id_uzytkownika_owner FK
        timestamp data_stworzenia
        string kolor_tablicy
    }

    TABLICE_UZYTKOWNICY {
        int id_uzytkownika FK
        int id_tablicy FK
        string rola "admin, member, viewer"
        timestamp data_dolaczenia
    }

    ZADANIA {
        int id_zadania PK
        int id_tablicy FK
        string tytul_zadania
        string opis_zadania
        timestamp data_stworzenia
        int id_uzytkownika_przypisanego FK
        int id_uzytkownika_tworcy_zadania FK
        string priorytet "wysoki, sredni, niski"
        string status "Todo, In Progress, In Review, Done"
        timestamp data_zakonczenia
        string kolumna_tablicy "dla Kanban"
    }

    KOMENTARZE {
        int id_komentarza PK
        int id_zadania FK
        string tresc_komentarza
        int id_uzytkownika FK
        timestamp data_utworzenia
        timestamp data_edycji
    }
```
