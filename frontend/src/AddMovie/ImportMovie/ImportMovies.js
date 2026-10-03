import React, { Component } from 'react';
import { Redirect, Route } from 'react-router-dom';
import ImportMovieConnector from 'AddMovie/ImportMovie/Import/ImportMovieConnector';
import ImportMovieSelectFolderConnector from 'AddMovie/ImportMovie/SelectFolder/ImportMovieSelectFolderConnector';
import Switch from 'Components/Router/Switch';
import getPathWithUrlBase from 'Utilities/getPathWithUrlBase';

function RedirectToMovies() {
  return <Redirect to={getPathWithUrlBase('/add/import/movies')} />;
}

class ImportMovies extends Component {

  //
  // Render

  render() {
    return (
      <Switch>
        <Route
          exact={true}
          path="/add/import"
          render={RedirectToMovies}
        />

        <Route
          exact={true}
          path="/add/import/movies"
          component={ImportMovieSelectFolderConnector}
        />

        <Route
          exact={true}
          path="/add/import/scenes"
          component={ImportMovieSelectFolderConnector}
        />

        <Route
          path="/add/import/movies/:rootFolderId"
          component={ImportMovieConnector}
        />

        <Route
          path="/add/import/scenes/:rootFolderId"
          component={ImportMovieConnector}
        />
      </Switch>
    );
  }
}

export default ImportMovies;
