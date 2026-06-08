from datetime import datetime
from typing import List, Optional
from sqlmodel import Field, Relationship, SQLModel
from .tablice_uzytkownicy import TabliceUzytkownicy

class Tablice(SQLModel, table=True):
    __tablename__ = "tablice"

    id_tablicy: Optional[int] = Field(default=None, primary_key=True)
    nazwa_tablicy: str
    opis_tablicy: Optional[str] = None
    id_uzytkownika_owner: int = Field(foreign_key="uzytkownicy.id_uzytkownika")
    data_stworzenia: datetime = Field(default_factory=datetime.now(timezone.utc))
    kolor_tablicy: Optional[str] = "#FFFFFF"

    owner: "Uzytkownicy" = Relationship(back_populates="stworzone_tablice")
    czlonkowie: List["Uzytkownicy"] = Relationship(
        back_populates="tablice_wspolpracownik", link_model=TabliceUzytkownicy
    )
    zadania: List["Zadania"] = Relationship(back_populates="tablica")