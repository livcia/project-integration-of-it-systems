from datetime import datetime
from typing import List, Optional
from sqlmodel import Field, Relationship, SQLModel

class Zadania(SQLModel, table=True):
    __tablename__ = "zadania"

    id_zadania: Optional[int] = Field(default=None, primary_key=True)
    id_tablicy: int = Field(foreign_key="tablice.id_tablicy")
    tytul_zadania: str
    opis_zadania: Optional[str] = None
    data_stworzenia: datetime = Field(default_factory=now(timezone.utc))
    id_uzytkownika_przypisanego: Optional[int] = Field(default=None, foreign_key="uzytkownicy.id_uzytkownika")
    id_uzytkownika_tworcy_zadania: int = Field(foreign_key="uzytkownicy.id_uzytkownika")
    priorytet: str = Field(default="sredni", description="wysoki, sredni, niski")
    status: str = Field(default="Todo", description="Todo, In Progress, In Review, Done")
    data_zakonczenia: Optional[datetime] = None
    kolumna_tablicy: Optional[str] = "Todo"

    tablica: "Tablice" = Relationship(back_populates="zadania")
    tworca: "Uzytkownicy" = Relationship(
        back_populates="stworzone_zadania", 
        sa_relationship_kwargs={"foreign_keys": "[Zadania.id_uzytkownika_tworcy_zadania]"}
    )
    przypisany_uzytkownik: Optional["Uzytkownicy"] = Relationship(
        back_populates="przypisane_zadania", 
        sa_relationship_kwargs={"foreign_keys": "[Zadania.id_uzytkownika_przypisanego]"}
    )
    komentarze: List["Komentarze"] = Relationship(back_populates="zadanie")